using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ramai.Api.Composition;
using Ramai.Application.Contracts.Identity;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountRecovery;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Identity.Persistence.Sessions;
using StackExchange.Redis;

namespace Ramai.IntegrationTests;

public sealed class WebSessionTests
{
    private static async Task<Guid> Prepare(AccountSecurityFixture fixture, bool confirmed = true)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password,
            new AccountRequestContext(IPAddress.Loopback, "session-setup"));
        var manager = services.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        if (confirmed) { user.EmailConfirmed = true; Assert.True((await manager.UpdateAsync(user)).Succeeded); }
        return user.Id;
    }

    [Fact]
    public void Auth_requires_explicit_key_configuration_and_is_disabled_by_default()
    {
        var services = new ServiceCollection();
        Assert.False(services.AddConfiguredWebIdentity(new ConfigurationBuilder().Build()));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["RAMAI_AUTH_ENABLED"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() => services.AddConfiguredWebIdentity(configuration));
    }

    [Fact]
    public async Task Foundation_does_not_expose_auth_without_explicit_configuration()
    {
        using var host = new WebApplicationFactory<Program>();
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/account/antiforgery")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Real_login_cookie_session_csrf_logout_and_cookie_replay()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var id = await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        var missing = await client.PostAsJsonAsync("/api/v1/account/login", new { email = fixture.Email, password = fixture.Password });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var login = await web.Login(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = WebSessionFixture.SessionCookie(login);
        var header = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-Ramai.Session="));
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", header, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("session-http-check", Assert.Single(login.Headers.GetValues("X-Correlation-ID")));
        var current = await client.GetAsync("/api/v1/account/session");
        var body = await current.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Contains(id.ToString(), body);
        Assert.DoesNotContain(fixture.Email, body);
        Assert.DoesNotContain("tenant", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/account/logout", null)).StatusCode);
        var logout = await WebSessionFixture.Write(client, "logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var replay = WebSessionFixture.Client(host, false);
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.GetAsync("/api/v1/account/session")).StatusCode);
        var logs = string.Join('\n', fixture.Logs.Entries);
        Assert.DoesNotContain(fixture.Password, logs);
        Assert.DoesNotContain(fixture.Email, logs);
        Assert.DoesNotContain(cookie.Split('=')[1], logs);
        Assert.Contains("session-http-check", logs);
    }

    [InfrastructureFact]
    public async Task Kestrel_https_health_and_real_socket_login_session_logout()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = web.NetworkClient();
        Assert.Equal("https", client.BaseAddress!.Scheme);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(client, "logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Invalid_unconfirmed_disabled_and_mfa_accounts_have_no_password_only_session()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture, false);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        var absent = await WebSessionFixture.Write(client, "login", new { email = "absent@example.invalid", password = fixture.Password });
        var unconfirmed = await web.Login(client);
        Assert.Equal(HttpStatusCode.Unauthorized, absent.StatusCode);
        Assert.Equal(absent.StatusCode, unconfirmed.StatusCode);
        Assert.Equal(await absent.Content.ReadAsStringAsync(), await unconfirmed.Content.ReadAsStringAsync());
        await using var scope = fixture.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true; user.TwoFactorEnabled = true;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.Login(client)).StatusCode);
        // D persists MFA failure state in a separate HTTP scope, updating Identity's concurrency stamp.
        await scope.ServiceProvider.GetRequiredService<IdentityStoreContext>().Entry(user).ReloadAsync();
        user.TwoFactorEnabled = false; user.IsActive = false;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.Login(client)).StatusCode);
    }

    [InfrastructureFact]
    public async Task Revoke_all_invalidates_another_real_session_after_commit()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var first = WebSessionFixture.Client(host);
        using var second = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(first)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(second)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(first, "revoke-all")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Password_reset_and_account_deactivation_revoke_existing_sessions()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(true);
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        await using var scope = fixture.Services.CreateAsyncScope();
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        var context = new AccountRequestContext(IPAddress.Loopback, "session-reset");
        await recovery.RequestResetAsync(fixture.Email, "/", context);
        var message = Assert.Single(fixture.Inbox.Messages);
        var query = QueryHelpers.ParseQuery(message.Link.Query);
        var newPassword = "quiet stars carry silver gardens " + Guid.NewGuid().ToString("N");
        var reset = await recovery.ResetPasswordAsync(Guid.Parse(query["userId"].ToString()), query["code"].ToString(), newPassword, context);
        Assert.Equal(AccountOperationStatus.Accepted, reset.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        using var replacement = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(replacement, "login", new { email = fixture.Email, password = newPassword })).StatusCode);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.IsActive = false;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replacement.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Idle_expiry_and_renewal_use_the_configured_clock()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        web.Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        web.Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        web.Clock.Advance(TimeSpan.FromMinutes(31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Absolute_expiry_is_not_extended_by_continuous_activity()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        for (var step = 0; step < 29; step++)
        {
            web.Clock.Advance(TimeSpan.FromMinutes(24));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        }
        web.Clock.Advance(TimeSpan.FromMinutes(24));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Ticket_ciphertext_tampering_and_atomic_renewal_do_not_restore_revoked_access()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var userId = await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        var login = await web.Login(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var key = fixture.Redis.GetServer(fixture.Redis.GetEndPoints()[0])
            .Keys(pattern: $"ramai:{{{fixture.RateNamespace}}}:session:*").Single();
        var value = await fixture.Redis.GetDatabase().StringGetAsync(key);
        var text = System.Text.Encoding.UTF8.GetString((byte[])value!);
        Assert.DoesNotContain(fixture.Email, text);
        Assert.DoesNotContain(fixture.Password, text);
        Assert.DoesNotContain(userId.ToString(), text);
        var store = host.Services.GetRequiredService<RedisSessionTicketStore>();
        var reference = key.ToString().Split(':')[^1];
        var ticket = (await store.RetrieveAsync(reference))!;
        await store.RemoveAsync(reference);
        await store.RenewAsync(reference, ticket);
        Assert.Null(await store.RetrieveAsync(reference));
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var racingKey = await store.StoreAsync(ticket);
            await Task.WhenAll(store.RemoveAsync(racingKey), store.RenewAsync(racingKey, ticket));
            Assert.Null(await store.RetrieveAsync(racingKey));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(AccountSessionClaims.Started, web.Clock.GetUtcNow().ToUnixTimeSeconds().ToString())], AccountSessionClaims.Scheme));
        var secondKey = await store.StoreAsync(new AuthenticationTicket(principal,
            new AuthenticationProperties { ExpiresUtc = web.Clock.GetUtcNow().AddMinutes(30) }, AccountSessionClaims.Scheme));
        await fixture.Redis.GetDatabase().StringSetAsync($"ramai:{{{fixture.RateNamespace}}}:session:{secondKey}", new byte[40], TimeSpan.FromMinutes(1));
        Assert.Null(await store.RetrieveAsync(secondKey));
        using var tampered = WebSessionFixture.Client(host, false);
        tampered.DefaultRequestHeaders.Add("Cookie", WebSessionFixture.SessionCookie(login) + "tampered");
        Assert.Equal(HttpStatusCode.Unauthorized, (await tampered.GetAsync("/api/v1/account/session")).StatusCode);
    }

    [InfrastructureFact]
    public async Task Shared_protected_keyring_survives_host_restart_but_foreign_keyring_does_not()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var first = web.CreateHost();
        using var client = WebSessionFixture.Client(first);
        var login = await web.Login(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = WebSessionFixture.SessionCookie(login);
        first.Dispose();
        using var restarted = WebSessionFixture.Client(web.CreateHost(), false);
        restarted.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await restarted.GetAsync("/api/v1/account/session")).StatusCode);
        using var foreign = WebSessionFixture.Client(web.CreateHost(foreignKeyring: true), false);
        foreign.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await foreign.GetAsync("/api/v1/account/session")).StatusCode);
        var files = Directory.GetFiles(Path.Combine(web.DirectoryPath, "ring"), "key-*.xml");
        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.Contains("encryptedSecret", File.ReadAllText(file)));
    }

    [InfrastructureFact]
    public async Task Transport_csrf_origin_and_body_limits_are_enforced()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        var body = new { email = fixture.Email, password = fixture.Password };
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "login", body, token: "invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "login", body, origin: "https://attacker.invalid")).StatusCode);
        using var plain = WebSessionFixture.Client(host);
        plain.BaseAddress = new Uri("http://ramai.example.invalid");
        Assert.Equal(HttpStatusCode.BadRequest, (await plain.GetAsync("/api/v1/account/antiforgery")).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync("/api/v1/account/login", new StringContent(new string('x', 5000)))).StatusCode);
    }

    [InfrastructureFact]
    public async Task Http_account_rate_limit_cannot_be_bypassed_with_forwarded_headers()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        for (var index = 0; index < 11; index++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"192.0.2.{index + 1}");
            var response = await WebSessionFixture.Write(client, "login", new { email = fixture.Email, password = fixture.Password });
            Assert.Equal(index < 10 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
            if (index == 10) { Assert.Equal("60", Assert.Single(response.Headers.GetValues("Retry-After"))); }
        }
        // Account hopping with spoofed proxy headers still shares the real socket's
        // 120/min origin budget. Eleven requests above already consumed that budget.
        for (var index = 0; index < 110; index++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", $"198.51.100.{index + 1}");
            var response = await WebSessionFixture.Write(client, "login",
                new { email = $"absent-{index}@example.invalid", password = fixture.Password });
            Assert.Equal(index < 109 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [InfrastructureFact]
    public async Task Redis_and_database_failures_return_generic_503_without_granting_access()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        var login = await web.Login(client);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var badDatabase = WebSessionFixture.Client(web.CreateHost("ramai_absent_" + Guid.NewGuid().ToString("N")), false);
        badDatabase.DefaultRequestHeaders.Add("Cookie", WebSessionFixture.SessionCookie(login));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await badDatabase.GetAsync("/api/v1/account/session")).StatusCode);
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var badRedis = WebSessionFixture.Client(web.CreateHost(redisPortOverride: closedPort.ToString()), false);
        badRedis.DefaultRequestHeaders.Add("Cookie", WebSessionFixture.SessionCookie(login));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await badRedis.GetAsync("/api/v1/account/session")).StatusCode);
        host.Services.GetRequiredService<IConnectionMultiplexer>().Dispose();
        var unavailable = await client.GetAsync("/api/v1/account/session");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.True(Guid.TryParseExact(Assert.Single(unavailable.Headers.GetValues("X-Correlation-ID")), "N", out _));
        Assert.DoesNotContain(fixture.Password, await unavailable.Content.ReadAsStringAsync());
    }
}
