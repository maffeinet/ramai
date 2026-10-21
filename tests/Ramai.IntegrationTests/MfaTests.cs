using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;

namespace Ramai.IntegrationTests;

public sealed class MfaTests
{
    private static async Task Prepare(AccountSecurityFixture fixture)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password,
            new AccountRequestContext(IPAddress.Loopback, "mfa-setup"));
        var manager = services.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
    }

    private static async Task<string> Begin(HttpClient client, AccountSecurityFixture fixture, string? code = null,
        string? recoveryCode = null)
    {
        var response = await WebSessionFixture.Write(client, "mfa/enrollment", new { password = fixture.Password, code, recoveryCode });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        return (await Json(response)).GetProperty("sharedKey").GetString()!;
    }

    private static async Task<string[]> Confirm(HttpClient client, AccountSecurityFixture fixture, string key, string? code = null)
    {
        var response = await WebSessionFixture.Write(client, "mfa/confirm", new { password = fixture.Password, code = code ?? Totp(key) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await Json(response)).GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(10, codes.Length);
        Assert.Equal(10, codes.Distinct().Count());
        return codes;
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, AccountSecurityFixture fixture,
        string? code = null, string? recoveryCode = null) => WebSessionFixture.Write(client, "login",
            new { email = fixture.Email, password = fixture.Password, code, recoveryCode });

    [InfrastructureFact]
    public async Task Enrollment_totp_login_replay_recovery_revocation_and_secret_redaction()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        using var previous = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(previous)).StatusCode);
        var key = await Begin(client, fixture);
        var consumedCode = Totp(key);
        var codes = await Confirm(client, fixture, key, consumedCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await previous.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, fixture, consumedCode)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, Totp(key, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(previous, fixture, recoveryCode: codes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        using var replay = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(replay, fixture, recoveryCode: codes[0])).StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityStoreContext>();
        var stored = await db.UserTokens.AsNoTracking().Select(x => x.Value!).ToArrayAsync();
        foreach (var value in stored)
        {
            Assert.DoesNotContain(key, value);
            foreach (var recovery in codes) { Assert.DoesNotContain(recovery, value); }
        }
        var logs = string.Join('\n', fixture.Logs.Entries);
        Assert.DoesNotContain(key, logs);
        Assert.DoesNotContain(fixture.Password, logs);
        Assert.DoesNotContain(Totp(key), logs);
        foreach (var recovery in codes) { Assert.DoesNotContain(recovery, logs); }
        Assert.Contains("Account MFA operation", logs);
        Assert.Contains("session-http-check", logs);
    }

    [InfrastructureFact]
    public async Task Concurrent_recovery_consumption_is_atomic_across_hosts()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var owner = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(owner)).StatusCode);
        var codes = await Confirm(owner, fixture, await Begin(owner, fixture));
        using var first = WebSessionFixture.Client(web.CreateHost());
        using var second = WebSessionFixture.Client(web.CreateHost());
        var results = await Task.WhenAll(Login(first, fixture, recoveryCode: codes[0]), Login(second, fixture, recoveryCode: codes[0]));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }

    [InfrastructureFact]
    public async Task Concurrent_totp_consumption_is_atomic_across_hosts()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var owner = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(owner)).StatusCode);
        var key = await Begin(owner, fixture);
        await Confirm(owner, fixture, key);
        using var first = WebSessionFixture.Client(web.CreateHost());
        using var second = WebSessionFixture.Client(web.CreateHost());
        var code = Totp(key, 1);
        var results = await Task.WhenAll(Login(first, fixture, code), Login(second, fixture, code));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Unauthorized);
    }

    [InfrastructureFact]
    public async Task Five_mfa_failures_lock_for_fifteen_minutes_despite_correct_password()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        for (var i = 0; i < 4; i++)
        { Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, fixture, recoveryCode: "invalid")).StatusCode); }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(client, fixture, recoveryCode: "invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
        web.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
    }

    [InfrastructureTheory]
    [InlineData(-10)]
    [InlineData(10)]
    public async Task Expired_and_far_future_totp_do_not_enable_mfa(int offset)
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var key = await Begin(client, fixture);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = Totp(key, offset) })).StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IdentityStoreContext>().Users.SingleAsync()).TwoFactorEnabled);
        await Confirm(client, fixture, key);
    }

    [InfrastructureFact]
    public async Task Enrollment_expires_and_requires_session_csrf_password_and_origin()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.Unauthorized, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = fixture.Password })).StatusCode);
        await web.Login(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/account/mfa/enrollment", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = fixture.Password }, origin: "https://hostile.invalid")).StatusCode);
        var key = await Begin(client, fixture);
        web.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = Totp(key) })).StatusCode);
    }

    [InfrastructureFact]
    public async Task Regenerated_recovery_codes_invalidate_previous_set_and_sessions()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
        var response = await WebSessionFixture.Write(client, "mfa/recovery-codes",
            new { password = fixture.Password, recoveryCode = codes[1] });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var replacement = (await Json(response)).GetProperty("recoveryCodes")[0].GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, fixture, recoveryCode: codes[2])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: replacement)).StatusCode);
    }

    [InfrastructureFact]
    public async Task Replacement_authenticator_is_not_active_until_confirmed_and_revokes_old_key()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var old = await Begin(client, fixture);
        var codes = await Confirm(client, fixture, old);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
        var replacement = await Begin(client, fixture, Totp(old, 1));
        Assert.NotEqual(old, replacement);
        await Confirm(client, fixture, replacement);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, fixture, Totp(old, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, fixture, recoveryCode: codes[1])).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, Totp(replacement, 1))).StatusCode);
    }

    [InfrastructureFact]
    public async Task Corrupted_mfa_material_fails_closed_without_dependency_details()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var key = await Begin(client, fixture);
        await Confirm(client, fixture, key);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityStoreContext>();
            var token = await db.UserTokens.SingleAsync(x => x.Name == "AuthenticatorKey");
            token.Value = "invalid-protected-value";
            await db.SaveChangesAsync();
        }
        var response = await Login(client, fixture, Totp(key, 1));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("session-http-check", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.DoesNotContain(key, await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    [InfrastructureFact]
    public async Task Wrong_confirmation_attempts_are_persisted_and_cannot_be_retried_without_limit()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var key = await Begin(client, fixture);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/confirm",
                new { password = fixture.Password, code = "bad" })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = Totp(key) })).StatusCode);
    }

    [InfrastructureFact]
    public async Task Reissuing_enrollment_invalidates_previous_pending_secret()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var old = await Begin(client, fixture);
        var current = await Begin(client, fixture);
        Assert.NotEqual(old, current);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = Totp(old) })).StatusCode);
        await Confirm(client, fixture, current);
    }

    [InfrastructureFact]
    public async Task Enrollment_rate_limit_applies_even_when_password_and_session_are_valid()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        for (var i = 0; i < 10; i++) { await Begin(client, fixture); }
        var response = await WebSessionFixture.Write(client, "mfa/enrollment", new { password = fixture.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    [InfrastructureFact]
    public async Task Foreign_keyring_cannot_decrypt_mfa_material_even_with_correct_password_and_recovery_code()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        using var foreign = WebSessionFixture.Client(web.CreateHost(foreignKeyring: true));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Login(foreign, fixture, recoveryCode: codes[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await foreign.GetAsync("/api/v1/account/session")).StatusCode);
        using var restarted = WebSessionFixture.Client(web.CreateHost());
        Assert.Equal(HttpStatusCode.NoContent, (await Login(restarted, fixture, recoveryCode: codes[0])).StatusCode);
    }

    [InfrastructureFact]
    public async Task Security_stamp_change_invalidates_pending_enrollment_not_only_old_session()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var old = await Begin(client, fixture);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByEmailAsync(fixture.Email))!;
            Assert.True((await manager.UpdateSecurityStampAsync(user)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/confirm",
            new { password = fixture.Password, code = Totp(old) })).StatusCode);
    }

    [InfrastructureFact]
    public async Task Lost_authenticator_can_be_replaced_with_recovery_without_password_only_downgrade()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
        var replacement = await Begin(client, fixture, recoveryCode: codes[1]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[2])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        await Confirm(client, fixture, replacement);
        Assert.Equal(HttpStatusCode.Unauthorized, (await web.Login(client)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, Totp(replacement, 1))).StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<IdentityStoreContext>().Users.SingleAsync()).TwoFactorEnabled);
    }

    [InfrastructureFact]
    public async Task Last_recovery_code_can_restore_device_and_recovery_step_up_is_single_use()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByEmailAsync(fixture.Email))!;
            foreach (var code in codes.Take(9)) { Assert.True((await manager.RedeemTwoFactorRecoveryCodeAsync(user, code)).Succeeded); }
            Assert.Equal(1, await manager.CountRecoveryCodesAsync(user));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[9])).StatusCode);
        var key = await Begin(client, fixture);
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = fixture.Password })).StatusCode);
        await Confirm(client, fixture, key);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, Totp(key, 1))).StatusCode);
    }

    [InfrastructureFact]
    public async Task Recovery_step_up_expires_after_five_minutes_and_is_not_a_password_only_bypass()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = WebSessionFixture.Client(web.CreateHost());
        await web.Login(client);
        var codes = await Confirm(client, fixture, await Begin(client, fixture));
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, recoveryCode: codes[0])).StatusCode);
        web.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.BadRequest, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = fixture.Password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebSessionFixture.Write(client, "mfa/enrollment",
            new { password = fixture.Password, recoveryCode = codes[1] })).StatusCode);
    }

    [InfrastructureFact]
    public async Task Kestrel_real_https_enrollment_totp_session_and_logout()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await Prepare(fixture);
        using var web = new WebSessionFixture(fixture);
        using var client = web.NetworkClient();
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        var key = await Begin(client, fixture);
        await Confirm(client, fixture, key);
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, fixture, Totp(key, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(client, "logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/session")).StatusCode);
    }

    // Independent RFC 6238 test client only. Production uses Identity's standard token provider.
    internal static string Totp(string key, int offset = 0)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var c in key)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); }
        }
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + offset);
        var hash = HMACSHA1.HashData(bytes.ToArray(), counter);
        var start = hash[^1] & 15;
        var number = BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(start, 4)) & int.MaxValue;
        return (number % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
