using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ramai.Application.Contracts.Identity;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Identity.Persistence.Sessions;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class WorkspaceHttpTests
{
    private static DbContextOptions<TenantsStoreContext> Options(string connection) =>
        new DbContextOptionsBuilder<TenantsStoreContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")).Options;
    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task<HttpResponseMessage> Write(HttpClient client, string path, object? body,
        HttpMethod? method = null, bool csrf = true, string? origin = null, string? raw = null)
    {
        var token = csrf ? await WebSessionFixture.Csrf(client) : null;
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        if (token is not null) { request.Headers.Add("X-CSRF-TOKEN", token); }
        request.Headers.Add("Origin", origin ?? WebSessionFixture.Origin);
        request.Headers.Add("X-Correlation-ID", "workspace-http-check");
        request.Content = raw is null ? JsonContent.Create(body) : new StringContent(raw, Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    private static async Task Problem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var json = await Json(response);
        Assert.Equal((int)status, json.GetProperty("status").GetInt32());
        Assert.Equal(response.Headers.GetValues("X-Correlation-ID").Single(), json.GetProperty("correlationId").GetString());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    private sealed class Scenario(AccountSecurityFixture accounts, WebSessionFixture web, HttpClient client,
        IServiceProvider hostServices, Guid userId, Guid a, Guid b, Guid c, Guid requestId)
    {
        public AccountSecurityFixture Accounts { get; } = accounts;
        public WebSessionFixture Web { get; } = web;
        public HttpClient Client { get; } = client;
        public IServiceProvider HostServices { get; } = hostServices;
        public Guid UserId { get; } = userId;
        public Guid A { get; } = a;
        public Guid B { get; } = b;
        public Guid C { get; } = c;
        public Guid RequestId { get; } = requestId;
        public Task<HttpResponseMessage> Select(Guid id) => Write(Client, "/api/v1/workspaces/select", new { workspaceId = id });
    }

    private static async Task WithScenario(Func<Scenario, Task> test, MembershipRole roleInB = MembershipRole.Member,
        bool mfa = true, bool network = false)
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        Guid userId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password,
                new AccountRequestContext(IPAddress.Loopback, "workspace-http-setup"));
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByEmailAsync(fixture.Email))!;
            user.EmailConfirmed = true;
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
            userId = user.Id;
        }
        await using (var db = new TenantsStoreContext(Options(fixture.Connection))) { await db.Database.MigrateAsync(); }
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = network ? web.NetworkClient() : WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        if (mfa)
        {
            var begin = await WebSessionFixture.Write(client, "mfa/enrollment", new { password = fixture.Password });
            Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
            var key = (await Json(begin)).GetProperty("sharedKey").GetString()!;
            var confirm = await WebSessionFixture.Write(client, "mfa/confirm", new { password = fixture.Password, code = MfaTests.Totp(key) });
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
            var recovery = (await Json(confirm)).GetProperty("recoveryCodes")[0].GetString()!;
            var login = await WebSessionFixture.Write(client, "login", new { email = fixture.Email, password = fixture.Password, recoveryCode = recovery });
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        }
        var requestId = Guid.NewGuid();
        Guid a;
        if (mfa)
        {
            var created = await Write(client, "/api/v1/workspaces", new { name = "Synthetic A", requestId });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            a = (await Json(created)).GetProperty("workspaceId").GetGuid();
        }
        else { a = Guid.NewGuid(); }
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        await using (var setup = new TenantsStoreContext(Options(fixture.Connection)))
        {
            if (!mfa) { setup.Tenants.Add(Tenant.Create(a, "Synthetic A", userId)); }
            var other = Tenant.Create(b, "Synthetic B", Guid.NewGuid());
            other.AddMember(userId, roleInB);
            setup.Tenants.Add(other);
            setup.Tenants.Add(Tenant.Create(c, "Synthetic C", Guid.NewGuid()));
            await setup.SaveChangesAsync();
        }
        await test(new(fixture, web, client, host.Services, userId, a, b, c, requestId));
    }

    [InfrastructureFact]
    public async Task Profile_selection_provisioning_settings_and_IDOR_follow_server_context()
    {
        await WithScenario(async s =>
        {
            var profile = await Json(await s.Client.GetAsync("/api/v1/profile"));
            Assert.Equal(s.UserId, profile.GetProperty("userId").GetGuid());
            Assert.True(profile.GetProperty("mfaVerified").GetBoolean());
            Assert.Equal(3, profile.EnumerateObject().Count());
            await Problem(await s.Client.GetAsync("/api/v1/workspace"), HttpStatusCode.Conflict);
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.A)).StatusCode);
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/workspace?tenantId=" + s.B))
            {
                request.Headers.Add("X-Tenant-ID", s.B.ToString());
                var own = await Json(await s.Client.SendAsync(request));
                Assert.Equal(s.A, own.GetProperty("workspaceId").GetGuid());
                Assert.Equal("Owner", own.GetProperty("role").GetString());
            }
            Assert.Equal(HttpStatusCode.OK, (await Write(s.Client, "/api/v1/workspace/settings", new { name = "Renamed A" }, HttpMethod.Put)).StatusCode);
            var replay = await Write(s.Client, "/api/v1/workspaces", new { name = "Synthetic A", requestId = s.RequestId });
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal(s.A, (await Json(replay)).GetProperty("workspaceId").GetGuid());
            await Problem(await Write(s.Client, "/api/v1/workspaces", new { name = "Different", requestId = s.RequestId }), HttpStatusCode.Conflict);
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.B)).StatusCode);
            Assert.Equal(s.B, (await Json(await s.Client.GetAsync("/api/v1/workspace"))).GetProperty("workspaceId").GetGuid());
            await Problem(await s.Client.GetAsync("/api/v1/workspace/settings"), HttpStatusCode.Forbidden);
            await Problem(await Write(s.Client, "/api/v1/workspace/settings", new { name = "Denied B" }, HttpMethod.Put), HttpStatusCode.Forbidden);
            await Problem(await s.Select(s.C), HttpStatusCode.Forbidden);
            await Problem(await s.Select(Guid.NewGuid()), HttpStatusCode.Forbidden);
            Assert.Equal(s.B, (await Json(await s.Client.GetAsync("/api/v1/workspace"))).GetProperty("workspaceId").GetGuid());
            await Problem(await s.Client.GetAsync("/api/v1/workspace/" + s.A), HttpStatusCode.NotFound);
            await Problem(await s.Client.GetAsync("/api/v1/workspace/" + Guid.NewGuid()), HttpStatusCode.NotFound);
            await using var check = new TenantsStoreContext(Options(s.Accounts.Connection));
            Assert.Equal("Renamed A", (await check.Tenants.SingleAsync(x => x.TenantId == s.A)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == s.B)).Name);
        });
    }

    [InfrastructureTheory]
    [InlineData(MembershipRole.Admin, HttpStatusCode.OK)]
    [InlineData(MembershipRole.Member, HttpStatusCode.Forbidden)]
    public async Task Http_role_matrix_is_authoritative(MembershipRole role, HttpStatusCode expected)
    {
        await WithScenario(async s =>
        {
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.B)).StatusCode);
            Assert.Equal(expected, (await s.Client.GetAsync("/api/v1/workspace/settings")).StatusCode);
            Assert.Equal(expected, (await Write(s.Client, "/api/v1/workspace/settings", new { name = "Changed B" }, HttpMethod.Put)).StatusCode);
        }, role);
    }

    [InfrastructureTheory]
    [InlineData("/api/v1/profile", "GET")]
    [InlineData("/api/v1/workspace", "GET")]
    [InlineData("/api/v1/workspace/settings", "GET")]
    [InlineData("/api/v1/workspace/settings", "PUT")]
    [InlineData("/api/v1/workspaces", "POST")]
    [InlineData("/api/v1/workspaces/select", "POST")]
    public async Task Anonymous_requests_are_401_problem_details(string path, string method)
    {
        await WithScenario(async s =>
        {
            using var anonymous = WebSessionFixture.Client(s.Web.CreateHost());
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
            await Problem(await anonymous.SendAsync(request), HttpStatusCode.Unauthorized);
        });
    }

    [InfrastructureTheory]
    [InlineData("{\"name\":\"Changed\",\"role\":\"Owner\"}", 400)]
    [InlineData("{\"name\":\"Changed\",\"tenantId\":\"00000000-0000-0000-0000-000000000001\"}", 400)]
    [InlineData("{\"name\":\"Changed\",\"userId\":\"00000000-0000-0000-0000-000000000001\"}", 400)]
    [InlineData("{\"name\":\"Changed\",\"Name\":\"Other\"}", 400)]
    [InlineData("{\"name\":{\"role\":\"Owner\"}}", 400)]
    [InlineData("{\"name\":\"   \"}", 400)]
    [InlineData("{", 400)]
    [InlineData("null", 400)]
    [InlineData("[]", 400)]
    public async Task Closed_json_contract_rejects_mass_assignment_and_invalid_input(string raw, int status)
    {
        await WithScenario(async s =>
        {
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.A)).StatusCode);
            await Problem(await Write(s.Client, "/api/v1/workspace/settings", null, HttpMethod.Put, raw: raw), (HttpStatusCode)status);
            Assert.Equal("Synthetic A", (await Json(await s.Client.GetAsync("/api/v1/workspace"))).GetProperty("name").GetString());
        });
    }

    [InfrastructureTheory]
    [InlineData("/api/v1/workspaces", "POST")]
    [InlineData("/api/v1/workspaces/select", "POST")]
    [InlineData("/api/v1/workspace/settings", "PUT")]
    public async Task All_mutations_reject_missing_csrf_hostile_origin_and_extra_grants(string path, string method)
    {
        await WithScenario(async s =>
        {
            await s.Select(s.A);
            await Problem(await Write(s.Client, path, new { }, new HttpMethod(method), csrf: false), HttpStatusCode.BadRequest);
            await Problem(await Write(s.Client, path, new { }, new HttpMethod(method), origin: "https://hostile.example.invalid"), HttpStatusCode.BadRequest);
            await Problem(await Write(s.Client, path, new { name = "Denied", workspaceId = s.B, role = "Owner", requestId = Guid.NewGuid() }, new HttpMethod(method)), HttpStatusCode.BadRequest);
            Assert.Equal("Synthetic A", (await Json(await s.Client.GetAsync("/api/v1/workspace"))).GetProperty("name").GetString());
        });
    }

    [InfrastructureFact]
    public async Task Body_limit_content_type_and_https_are_enforced()
    {
        await WithScenario(async s =>
        {
            await s.Select(s.A);
            await Problem(await Write(s.Client, "/api/v1/workspace/settings", null, HttpMethod.Put, raw: "{\"name\":\"" + new string('x', 5000) + "\"}"), HttpStatusCode.RequestEntityTooLarge);
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/workspace/settings") { Content = new StringContent("plain") };
            request.Headers.Add("X-CSRF-TOKEN", await WebSessionFixture.Csrf(s.Client));
            request.Headers.Add("Origin", WebSessionFixture.Origin);
            await Problem(await s.Client.SendAsync(request), HttpStatusCode.UnsupportedMediaType);
            await Problem(await s.Client.GetAsync("http://ramai.example.invalid/api/v1/profile"), HttpStatusCode.BadRequest);
        });
    }

    [InfrastructureFact]
    public async Task Missing_mfa_cannot_provision_or_select_privileged_membership()
    {
        await WithScenario(async s =>
        {
            await Problem(await s.Select(s.A), HttpStatusCode.Forbidden);
            await Problem(await Write(s.Client, "/api/v1/workspaces", new { name = "Denied", requestId = Guid.NewGuid() }), HttpStatusCode.Forbidden);
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.B)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync("/api/v1/workspace")).StatusCode);
            await Problem(await s.Client.GetAsync("/api/v1/workspace/settings"), HttpStatusCode.Forbidden);
        }, mfa: false);
    }

    [InfrastructureFact]
    public async Task Role_membership_and_account_revocation_are_observed_over_HTTP()
    {
        await WithScenario(async s =>
        {
            await s.Select(s.B);
            Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync("/api/v1/workspace/settings")).StatusCode);
            await using (var setup = new TenantsStoreContext(Options(s.Accounts.Connection)))
            {
                var member = await setup.Memberships.SingleAsync(x => x.TenantId == s.B && x.UserId == s.UserId);
                setup.Entry(member).Property(x => x.Role).CurrentValue = MembershipRole.Member;
                await setup.SaveChangesAsync();
            }
            await Problem(await s.Client.GetAsync("/api/v1/workspace/settings"), HttpStatusCode.Forbidden);
            await using (var setup = new TenantsStoreContext(Options(s.Accounts.Connection)))
            {
                var tenant = await setup.Tenants.Include(x => x.Memberships).SingleAsync(x => x.TenantId == s.B);
                tenant.DeactivateMember(s.UserId);
                await setup.SaveChangesAsync();
            }
            await Problem(await s.Client.GetAsync("/api/v1/workspace"), HttpStatusCode.Forbidden);
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.A)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(s.Client, "revoke-all")).StatusCode);
            await Problem(await s.Client.GetAsync("/api/v1/profile"), HttpStatusCode.Unauthorized);
        }, MembershipRole.Admin);
    }

    [InfrastructureFact]
    public async Task Selection_preserves_absolute_start_in_the_actual_redis_ticket()
    {
        await WithScenario(async s =>
        {
            var server = s.Accounts.Redis.GetServer(s.Accounts.Redis.GetEndPoints().Single());
            var prefix = $"ramai:{{{s.Accounts.RateNamespace}}}:session:";
            var key = server.Keys(pattern: prefix + "*").Single().ToString()[prefix.Length..];
            var store = s.HostServices.GetRequiredService<RedisSessionTicketStore>();
            var before = (await store.RetrieveAsync(key))!;
            s.Web.Clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.A)).StatusCode);
            var after = (await store.RetrieveAsync(key))!;
            Assert.Equal(before.Principal.FindFirstValue(AccountSessionClaims.Started), after.Principal.FindFirstValue(AccountSessionClaims.Started));
            Assert.Equal(s.A.ToString(), after.Principal.FindFirstValue("ramai.selected_tenant"));
        });
    }

    [InfrastructureFact]
    public async Task Provisioning_quota_is_distributed_across_hosts_and_returns_429()
    {
        await WithScenario(async s =>
        {
            var selection = await s.Select(s.A);
            var cookie = WebSessionFixture.SessionCookie(selection);
            using var other = WebSessionFixture.Client(s.Web.CreateHost());
            other.DefaultRequestHeaders.Add("Cookie", cookie);
            for (var index = 0; index < 9; index++)
            {
                var client = index % 2 == 0 ? other : s.Client;
                Assert.Equal(HttpStatusCode.OK, (await Write(client, "/api/v1/workspaces", new { name = "Synthetic A", requestId = s.RequestId })).StatusCode);
            }
            var limited = await Write(other, "/api/v1/workspaces", new { name = "Synthetic A", requestId = s.RequestId });
            await Problem(limited, HttpStatusCode.TooManyRequests);
            Assert.Equal("60", limited.Headers.RetryAfter!.ToString());
        });
    }

    [InfrastructureFact]
    public async Task Dependency_outage_is_sanitized_problem_and_logs_do_not_contain_credentials()
    {
        await WithScenario(async s =>
        {
            var response = await s.Select(s.A);
            var cookie = WebSessionFixture.SessionCookie(response);
            foreach (var host in new[] { s.Web.CreateHost(databaseOverride: "missing_" + Guid.NewGuid().ToString("N")), s.Web.CreateHost(redisPortOverride: "1") })
            {
                using var client = WebSessionFixture.Client(host, cookies: false);
                client.DefaultRequestHeaders.Add("Cookie", cookie);
                using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/workspace");
                request.Headers.Add("X-Correlation-ID", "workspace-dependency-check");
                var unavailable = await client.SendAsync(request);
                await Problem(unavailable, HttpStatusCode.ServiceUnavailable);
                var body = await unavailable.Content.ReadAsStringAsync();
                Assert.DoesNotContain(s.Accounts.Password, body);
                Assert.DoesNotContain(cookie, body);
                Assert.DoesNotContain("Exception", body);
            }
            var logs = string.Join('\n', s.Accounts.Logs.Entries);
            Assert.Contains("CorrelationId=workspace-http-check", logs);
            Assert.DoesNotContain(s.Accounts.Password, logs);
            Assert.DoesNotContain(cookie, logs);
            Assert.DoesNotContain(Convert.ToBase64String(s.Accounts.RateKey), logs);
        });
    }

    [InfrastructureFact]
    public async Task Workspace_flow_works_over_real_Kestrel_HTTPS()
    {
        await WithScenario(async s =>
        {
            Assert.Equal(HttpStatusCode.NoContent, (await s.Select(s.A)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await s.Client.GetAsync("/api/v1/workspace/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Write(s.Client, "/api/v1/workspace/settings", new { name = "Network A" }, HttpMethod.Put)).StatusCode);
        }, network: true);
    }
}
