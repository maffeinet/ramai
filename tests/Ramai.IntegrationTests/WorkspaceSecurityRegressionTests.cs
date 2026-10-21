using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed partial class WorkspaceHttpTests
{
    private sealed record SecurityActor(HttpClient Client, Guid UserId, Guid TenantId, string Cookie);

    private static async Task<SecurityActor> OtherActor(Scenario s)
    {
        var email = $"security-{Guid.NewGuid():N}@example.invalid";
        var password = "quiet rivers carry violet lanterns " + Guid.NewGuid().ToString("N");
        Guid id;
        string key;
        await using (var scope = s.HostServices.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = new AccountUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded);
            Assert.True((await manager.ResetAuthenticatorKeyAsync(user)).Succeeded);
            Assert.True((await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded);
            key = (await manager.GetAuthenticatorKeyAsync(user))!;
            id = user.Id;
        }
        var tenantId = Guid.NewGuid();
        await using (var db = new TenantsStoreContext(Options(s.Accounts.Connection)))
        {
            db.Tenants.Add(Tenant.Create(tenantId, "Security other company", id));
            await db.SaveChangesAsync();
        }
        var client = WebSessionFixture.Client(s.Web.CreateHost());
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(client, "login",
                new { email, password, code = MfaTests.Totp(key) })).StatusCode);
            var selected = await Write(client, "/api/v1/workspaces/select", new { workspaceId = tenantId });
            Assert.Equal(HttpStatusCode.NoContent, selected.StatusCode);
            return new(client, id, tenantId, WebSessionFixture.SessionCookie(selected));
        }
        catch { client.Dispose(); throw; }
    }

    [InfrastructureTheory]
    [InlineData("/api/v1/profile", "GET")]
    [InlineData("/api/v1/workspace", "GET")]
    [InlineData("/api/v1/workspace/settings", "GET")]
    [InlineData("/api/v1/workspace/settings", "PUT")]
    [InlineData("/api/v1/workspaces", "POST")]
    [InlineData("/api/v1/workspaces/select", "POST")]
    [InlineData("/api/v1/account/session", "GET")]
    [InlineData("/api/v1/account/logout", "POST")]
    [InlineData("/api/v1/account/revoke-all", "POST")]
    [InlineData("/api/v1/account/mfa/enrollment", "POST")]
    [InlineData("/api/v1/account/mfa/confirm", "POST")]
    [InlineData("/api/v1/account/mfa/recovery-codes", "POST")]
    public async Task Revoked_cookie_replay_is_401_on_every_protected_endpoint(string path, string method)
    {
        await WithScenario(async s =>
        {
            var cookie = WebSessionFixture.SessionCookie(await s.Select(s.A));
            var csrf = await WebSessionFixture.Csrf(s.Client);
            Assert.Equal(HttpStatusCode.NoContent, (await WebSessionFixture.Write(s.Client, "revoke-all")).StatusCode);
            using var replay = WebSessionFixture.Client(s.Web.CreateHost(), cookies: false);
            replay.DefaultRequestHeaders.Add("Cookie", cookie);
            using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
            request.Headers.Add("X-CSRF-TOKEN", csrf);
            request.Headers.Add("Origin", WebSessionFixture.Origin);
            var denied = await replay.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            Assert.Contains("no-store", denied.Headers.CacheControl!.ToString());
            SensitiveAssertions.Absent(await denied.Content.ReadAsStringAsync(), s.Accounts.Password, cookie, csrf, s.A.ToString());
        });
    }

    [InfrastructureFact]
    public async Task Public_surface_is_explicit_and_all_other_api_routes_require_authorization()
    {
        await WithScenario(s =>
        {
            var routes = s.HostServices.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
                .Where(x => x.RoutePattern.RawText!.StartsWith("/api/v1/", StringComparison.Ordinal)).ToArray();
            Assert.Equal(14, routes.Length); // 12 protected operations, login and antiforgery.
            foreach (var route in routes)
            {
                var path = route.RoutePattern.RawText!;
                var method = Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
                if (method == "GET")
                {
                    Assert.Contains(path, new[] { "/api/v1/account/antiforgery", "/api/v1/account/session",
                        "/api/v1/profile", "/api/v1/workspace", "/api/v1/workspace/settings" });
                }
                else { Assert.Equal(path == "/api/v1/workspace/settings" ? "PUT" : "POST", method); }
                if (path is "/api/v1/account/antiforgery" or "/api/v1/account/login") { continue; }
                Assert.NotEmpty(route.Metadata.GetOrderedMetadata<IAuthorizeData>());
                Assert.Null(route.Metadata.GetMetadata<IAllowAnonymous>());
            }
            return Task.CompletedTask;
        });
    }

    [InfrastructureFact]
    public async Task Csrf_token_from_another_subject_cannot_mutate_or_logout_the_victim()
    {
        await WithScenario(async s =>
        {
            var victimCookie = WebSessionFixture.SessionCookie(await s.Select(s.A));
            var other = await OtherActor(s);
            using var otherClient = other.Client;
            using var tokenClient = WebSessionFixture.Client(s.Web.CreateHost(), cookies: false);
            tokenClient.DefaultRequestHeaders.Add("Cookie", other.Cookie);
            var tokenResponse = await tokenClient.GetAsync("/api/v1/account/antiforgery");
            Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
            var foreignToken = (await Json(tokenResponse)).GetProperty("requestToken").GetString()!;
            var foreignCookie = tokenResponse.Headers.GetValues("Set-Cookie")
                .Single(x => x.StartsWith("__Host-Ramai.Antiforgery=", StringComparison.Ordinal)).Split(';')[0];
            using var victim = WebSessionFixture.Client(s.Web.CreateHost(), cookies: false);
            // Valid foreign token/cookie pair, but a different authenticated subject.
            victim.DefaultRequestHeaders.Add("Cookie", victimCookie + "; " + foreignCookie);
            foreach (var (path, method, body) in new (string, HttpMethod, object)[]
            {
                ("/api/v1/workspace/settings", HttpMethod.Put, new { name = "Denied foreign CSRF" }),
                ("/api/v1/workspaces/select", HttpMethod.Post, new { workspaceId = s.B }),
                ("/api/v1/workspaces", HttpMethod.Post, new { name = "Denied create", requestId = Guid.NewGuid() }),
                ("/api/v1/account/logout", HttpMethod.Post, new { }),
                ("/api/v1/account/revoke-all", HttpMethod.Post, new { }),
                ("/api/v1/account/mfa/enrollment", HttpMethod.Post, new { password = s.Accounts.Password })
            })
            {
                using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
                request.Headers.Add("X-CSRF-TOKEN", foreignToken);
                request.Headers.Add("Origin", WebSessionFixture.Origin);
                Assert.Equal(HttpStatusCode.BadRequest, (await victim.SendAsync(request)).StatusCode);
            }
            Assert.Equal(s.A, (await Json(await s.Client.GetAsync("/api/v1/workspace"))).GetProperty("workspaceId").GetGuid());
            await using var db = new TenantsStoreContext(Options(s.Accounts.Connection));
            Assert.Equal("Synthetic A", (await db.Tenants.SingleAsync(x => x.TenantId == s.A)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Concurrent_subjects_with_forged_tenant_headers_read_and_write_only_their_own_workspace()
    {
        await WithScenario(async s =>
        {
            await s.Select(s.A);
            var other = await OtherActor(s);
            using var otherClient = other.Client;
            async Task Exercise(HttpClient client, Guid own, Guid foreign, Guid userId, string name)
            {
                client.DefaultRequestHeaders.Add("X-Tenant-ID", foreign.ToString());
                client.DefaultRequestHeaders.Add("X-User-ID", Guid.NewGuid().ToString());
                for (var index = 0; index < 3; index++)
                {
                    var profile = await Json(await client.GetAsync($"/api/v1/profile?userId={Guid.NewGuid()}&tenantId={foreign}"));
                    Assert.Equal(userId, profile.GetProperty("userId").GetGuid());
                    var changed = await Write(client, $"/api/v1/workspace/settings?tenantId={foreign}", new { name }, HttpMethod.Put);
                    Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
                    Assert.Equal(own, (await Json(changed)).GetProperty("workspaceId").GetGuid());
                    var read = await Json(await client.GetAsync($"/api/v1/workspace?tenantId={foreign}"));
                    Assert.Equal(own, read.GetProperty("workspaceId").GetGuid());
                    Assert.Equal(name, read.GetProperty("name").GetString());
                }
                Assert.Equal(HttpStatusCode.Forbidden, (await Write(client, "/api/v1/workspaces/select", new { workspaceId = foreign })).StatusCode);
                Assert.Equal(own, (await Json(await client.GetAsync("/api/v1/workspace"))).GetProperty("workspaceId").GetGuid());
            }
            await Task.WhenAll(Exercise(s.Client, s.A, other.TenantId, s.UserId, "Own A only"),
                Exercise(otherClient, other.TenantId, s.A, other.UserId, "Own B only"));
            await using var db = new TenantsStoreContext(Options(s.Accounts.Connection));
            Assert.Equal("Own A only", (await db.Tenants.SingleAsync(x => x.TenantId == s.A)).Name);
            Assert.Equal("Own B only", (await db.Tenants.SingleAsync(x => x.TenantId == other.TenantId)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Provisioning_request_id_is_scoped_to_creator_not_another_account_receipt()
    {
        await WithScenario(async s =>
        {
            var other = await OtherActor(s);
            using var client = other.Client;
            var created = await Write(client, "/api/v1/workspaces", new { name = "Synthetic A", requestId = s.RequestId });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var own = (await Json(created)).GetProperty("workspaceId").GetGuid();
            Assert.NotEqual(s.A, own);
            Assert.Equal(HttpStatusCode.Forbidden, (await Write(client, "/api/v1/workspaces/select", new { workspaceId = s.A })).StatusCode);
            await using var db = new TenantsStoreContext(Options(s.Accounts.Connection));
            Assert.Equal(MembershipRole.Owner, (await db.Memberships.SingleAsync(x => x.TenantId == own && x.UserId == other.UserId)).Role);
            Assert.False(await db.Memberships.AnyAsync(x => x.TenantId == s.A && x.UserId == other.UserId));
        });
    }

    [InfrastructureTheory]
    [InlineData(31)]
    [InlineData(720)]
    public async Task Expired_selected_workspace_session_cannot_read_or_write_after_timeout(int minutes)
    {
        await WithScenario(async s =>
        {
            await s.Select(s.A);
            s.Web.Clock.Advance(TimeSpan.FromMinutes(minutes));
            await Problem(await s.Client.GetAsync("/api/v1/workspace"), HttpStatusCode.Unauthorized);
            await Problem(await Write(s.Client, "/api/v1/workspace/settings", new { name = "Expired write" }, HttpMethod.Put), HttpStatusCode.Unauthorized);
            await using var db = new TenantsStoreContext(Options(s.Accounts.Connection));
            Assert.Equal("Synthetic A", (await db.Tenants.SingleAsync(x => x.TenantId == s.A)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Captured_logs_omit_actual_credentials_cookies_csrf_mfa_material_and_business_body()
    {
        await WithScenario(async s =>
        {
            var cookie = WebSessionFixture.SessionCookie(await s.Select(s.A));
            var csrf = await WebSessionFixture.Csrf(s.Client);
            var bodyCanary = "PRIVATE-SYNTHETIC-BUSINESS-" + Guid.NewGuid().ToString("N");
            Assert.Equal(HttpStatusCode.OK, (await Write(s.Client, "/api/v1/workspace/settings", new { name = bodyCanary }, HttpMethod.Put)).StatusCode);
            await Problem(await s.Select(s.C), HttpStatusCode.Forbidden);
            await using var scope = s.HostServices.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByIdAsync(s.UserId.ToString()))!;
            var key = (await manager.GetAuthenticatorKeyAsync(user))!;
            var logs = string.Join('\n', s.Accounts.Logs.Entries);
            Assert.Contains("CorrelationId=workspace-http-check", logs);
            Assert.Contains("Workspace operation", logs);
            var databasePassword = new NpgsqlConnectionStringBuilder(s.Accounts.Connection).Password
                ?? throw new InvalidOperationException("Database test canary missing; values withheld.");
            SensitiveAssertions.Absent(logs, s.Accounts.Password, s.Accounts.Email, user.PasswordHash!, key,
                cookie, cookie[(cookie.IndexOf('=') + 1)..], csrf, bodyCanary, s.Accounts.Connection,
                databasePassword, Convert.ToBase64String(s.Accounts.RateKey));
        });
    }
}
