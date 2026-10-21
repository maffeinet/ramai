using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class TenantContextIntegrationTests
{
    private static TenantsStoreContext Tenants(string connection) => new(new DbContextOptionsBuilder<TenantsStoreContext>()
        .UseNpgsql(connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")).Options);

    private static async Task<Guid> Prepare(AccountSecurityFixture fixture)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password,
            new AccountRequestContext(IPAddress.Loopback, "tenant-context-setup"));
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        return user.Id;
    }

    private static TenantContextResolver Resolver(IServiceProvider services, TenantsStoreContext db, TimeProvider clock) =>
        new(services.GetRequiredService<IAccountSessionValidator>(), new TenantMembershipReader(db), clock);

    private static async Task<ClaimsPrincipal> Login(IServiceProvider services, AccountSecurityFixture fixture, string? recovery = null)
    {
        var result = await services.GetRequiredService<IAccountLoginService>().CheckAsync(fixture.Email, fixture.Password,
            IPAddress.Loopback, "tenant-context-login", recoveryCode: recovery);
        Assert.Equal(LoginOutcome.Accepted, result.Outcome);
        return Assert.IsType<ClaimsPrincipal>(result.Principal);
    }

    [InfrastructureFact]
    public async Task Real_mfa_identity_is_owner_in_A_member_in_B_and_stranger_in_C()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var userId = await Prepare(fixture);
        await using var tenants = Tenants(fixture.Connection);
        await tenants.Database.MigrateAsync();
        var a = Tenant.Create(Guid.NewGuid(), "Synthetic A", userId);
        var b = Tenant.Create(Guid.NewGuid(), "Synthetic B", Guid.NewGuid());
        b.AddMember(userId);
        var c = Tenant.Create(Guid.NewGuid(), "Synthetic C", Guid.NewGuid());
        tenants.AddRange(a, b, c);
        await tenants.SaveChangesAsync();
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        await using var scope = host.Services.CreateAsyncScope();
        var principal = await Login(scope.ServiceProvider, fixture);
        var resolver = Resolver(scope.ServiceProvider, tenants, web.Clock);
        Assert.Equal(TenantContextOutcome.MfaRequired, (await resolver.SelectAsync(principal, a.TenantId)).Outcome);
        Assert.Equal(TenantContextOutcome.Denied, (await resolver.SelectAsync(principal, c.TenantId)).Outcome);
        var begin = await WebSessionFixture.Write(client, "mfa/enrollment", new { password = fixture.Password });
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var key = JsonSerializer.Deserialize<JsonElement>(await begin.Content.ReadAsStringAsync()).GetProperty("sharedKey").GetString()!;
        var confirm = await WebSessionFixture.Write(client, "mfa/confirm", new { password = fixture.Password, code = MfaTests.Totp(key) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var recovery = JsonSerializer.Deserialize<JsonElement>(await confirm.Content.ReadAsStringAsync()).GetProperty("recoveryCodes")[0].GetString()!;
        // New request scope; password and recovery are verified by the real Identity service, not a fake principal.
        await using var fresh = host.Services.CreateAsyncScope();
        principal = await Login(fresh.ServiceProvider, fixture, recovery);
        resolver = Resolver(fresh.ServiceProvider, tenants, web.Clock);
        var selected = await resolver.SelectAsync(principal, a.TenantId);
        Assert.Equal(TenantAccessRole.Owner, selected.Context!.Role);
        Assert.True(selected.Context.MfaVerified);
        Assert.Equal(TenantContextOutcome.Accepted, (await resolver.ResolveAsync(selected.SelectedPrincipal!)).Outcome);
        var switched = await resolver.SelectAsync(selected.SelectedPrincipal!, b.TenantId);
        Assert.Equal(TenantAccessRole.Member, switched.Context!.Role);
        Assert.Equal(b.TenantId, switched.Context.TenantId);
        Assert.Equal(a.TenantId.ToString(), selected.SelectedPrincipal!.FindFirstValue(TenantSessionClaims.SelectedTenant));
        Assert.Equal(TenantContextOutcome.Denied, (await resolver.SelectAsync(switched.SelectedPrincipal!, c.TenantId)).Outcome);
    }

    [InfrastructureFact]
    public async Task Membership_revocation_and_role_changes_are_reloaded_not_cached_in_session()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var userId = await Prepare(fixture);
        await using var tenants = Tenants(fixture.Connection);
        await tenants.Database.MigrateAsync();
        var a = Tenant.Create(Guid.NewGuid(), "Synthetic membership", Guid.NewGuid());
        a.AddMember(userId);
        tenants.Add(a);
        await tenants.SaveChangesAsync();
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        await using var scope = host.Services.CreateAsyncScope();
        var resolver = Resolver(scope.ServiceProvider, tenants, web.Clock);
        var selected = (await resolver.SelectAsync(await Login(scope.ServiceProvider, fixture), a.TenantId)).SelectedPrincipal!;
        // Test setup mutates the owned fixture row only; no production role-management feature is added.
        await using (var other = Tenants(fixture.Connection))
        {
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE tenants.\"Memberships\" SET \"Role\" = 1 WHERE \"TenantId\" = {a.TenantId} AND \"UserId\" = {userId}");
        }
        Assert.Equal(TenantContextOutcome.MfaRequired, (await resolver.ResolveAsync(selected)).Outcome);
        a.DeactivateMember(userId);
        await tenants.SaveChangesAsync();
        var rejected = await resolver.ResolveAsync(selected);
        Assert.Equal(TenantContextOutcome.Denied, rejected.Outcome);
        Assert.Null(rejected.Context);
    }

    [InfrastructureTheory]
    [InlineData("inactive")]
    [InlineData("unconfirmed")]
    [InlineData("revoked")]
    [InlineData("locked")]
    public async Task Account_changes_reject_existing_selected_context(string change)
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var userId = await Prepare(fixture);
        await using var tenants = Tenants(fixture.Connection);
        await tenants.Database.MigrateAsync();
        var a = Tenant.Create(Guid.NewGuid(), "Synthetic A", Guid.NewGuid());
        a.AddMember(userId);
        tenants.Add(a);
        await tenants.SaveChangesAsync();
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        await using var scope = host.Services.CreateAsyncScope();
        var resolver = Resolver(scope.ServiceProvider, tenants, web.Clock);
        var selected = (await resolver.SelectAsync(await Login(scope.ServiceProvider, fixture), a.TenantId)).SelectedPrincipal!;
        await using (var other = host.Services.CreateAsyncScope())
        {
            var manager = other.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByIdAsync(userId.ToString()))!;
            switch (change)
            {
                case "inactive": user.IsActive = false; break;
                case "unconfirmed": user.EmailConfirmed = false; break;
                case "revoked": Assert.True((await manager.UpdateSecurityStampAsync(user)).Succeeded); break;
                case "locked": user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15); break;
            }
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
        }
        Assert.Equal(TenantContextOutcome.Unauthenticated, (await resolver.ResolveAsync(selected)).Outcome);
    }

    [InfrastructureFact]
    public async Task Concurrent_requests_have_separate_contexts_and_never_change_the_input_principal()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var userId = await Prepare(fixture);
        await using var setup = Tenants(fixture.Connection);
        await setup.Database.MigrateAsync();
        var a = Tenant.Create(Guid.NewGuid(), "Synthetic A", Guid.NewGuid());
        var b = Tenant.Create(Guid.NewGuid(), "Synthetic B", Guid.NewGuid());
        a.AddMember(userId); b.AddMember(userId);
        setup.AddRange(a, b);
        await setup.SaveChangesAsync();
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        await using var loginScope = host.Services.CreateAsyncScope();
        var principal = await Login(loginScope.ServiceProvider, fixture);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            await using var db = Tenants(fixture.Connection);
            var resolver = Resolver(scope.ServiceProvider, db, web.Clock);
            var expected = index % 2 == 0 ? a.TenantId : b.TenantId;
            var selection = await resolver.SelectAsync(principal, expected);
            Assert.Equal(expected, (await resolver.ResolveAsync(selection.SelectedPrincipal!)).Context!.TenantId);
        }));
        Assert.Null(principal.FindFirst(TenantSessionClaims.SelectedTenant));
    }
}
