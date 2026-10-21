using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class WorkspaceProvisioningIntegrationTests
{
    private static DbContextOptions<TenantsStoreContext> Options(string connection, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<TenantsStoreContext>().UseNpgsql(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"));
        if (interceptor is not null) { builder.AddInterceptors(interceptor); }
        return builder.Options;
    }

    private static WorkspaceProvisioningStore Writer(AccountSecurityFixture fixture, IServiceProvider services, IInterceptor? interceptor = null) =>
        new(new PooledDbContextFactory<TenantsStoreContext>(Options(fixture.Connection, interceptor)),
            services.GetRequiredService<ILogger<WorkspaceProvisioningStore>>());

    private static WorkspaceProvisioningService Service(AccountSecurityFixture fixture, IServiceProvider services, TimeProvider clock,
        IInterceptor? interceptor = null) => new(services.GetRequiredService<IAccountSessionValidator>(),
            new UserAccountReader(services.GetRequiredService<IdentityStoreContext>()), Writer(fixture, services, interceptor), clock);

    [InfrastructureFact]
    public async Task Real_verified_mfa_subject_gets_one_owner_and_retries_do_not_duplicate_or_rename()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var service = Service(fixture, services, web.Clock);
            var key = Guid.NewGuid();
            var first = await service.CreateAsync(principal, " Synthetic private workspace ", key, "workspace-provision-check");
            Assert.Equal(WorkspaceProvisioningOutcome.Created, first.Outcome);
            var replay = await service.CreateAsync(principal, "Synthetic private workspace", key, "workspace-provision-check");
            Assert.Equal(WorkspaceProvisioningOutcome.Replayed, replay.Outcome);
            Assert.Equal(first.TenantId, replay.TenantId);
            var conflict = await service.CreateAsync(principal, "Different name", key, "workspace-provision-check");
            Assert.Equal(WorkspaceProvisioningOutcome.Conflict, conflict.Outcome);
            Assert.Null(conflict.TenantId);
            await using var db = new TenantsStoreContext(Options(fixture.Connection));
            var tenant = await db.Tenants.Include(x => x.Memberships).SingleAsync();
            Assert.Equal("Synthetic private workspace", tenant.Name);
            var owner = Assert.Single(tenant.Memberships);
            Assert.Equal(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), owner.UserId);
            Assert.Equal(MembershipRole.Owner, owner.Role);
            Assert.Equal(tenant.TenantId, owner.TenantId);
            Assert.Equal(1, await ReceiptCount(db));
            var logs = string.Join('\n', fixture.Logs.Entries);
            Assert.Contains("workspace-provision-check", logs);
            Assert.Contains("Workspace provisioning outcome Created", logs);
            Assert.DoesNotContain("Synthetic private workspace", logs);
            Assert.Null(principal.FindFirst(TenantSessionClaims.SelectedTenant));
        });
    }

    [InfrastructureFact]
    public async Task Concurrent_duplicate_requests_commit_exactly_one_workspace_and_receipt()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var owner = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var request = Guid.NewGuid();
            // Persistence race only: independent contexts, same already-verified subject.
            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
                Writer(fixture, services).CreateAsync(owner, "Concurrent synthetic", request, "workspace-race-check")));
            Assert.Single(results, x => x.Outcome == WorkspaceProvisioningOutcome.Created);
            Assert.Equal(11, results.Count(x => x.Outcome == WorkspaceProvisioningOutcome.Replayed));
            Assert.Single(results.Select(x => x.TenantId).Distinct());
            await using var db = new TenantsStoreContext(Options(fixture.Connection));
            Assert.Equal(1, await db.Tenants.CountAsync());
            Assert.Equal(1, await db.Memberships.CountAsync());
            Assert.Equal(1, await ReceiptCount(db));
        });
    }

    [InfrastructureFact]
    public async Task Failure_after_writes_before_commit_rolls_back_all_three_rows_and_retry_succeeds()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var key = Guid.NewGuid();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(fixture, services, web.Clock, new FailAfterSave())
                .CreateAsync(principal, "Synthetic rollback", key, "workspace-rollback-check"));
            await using var db = new TenantsStoreContext(Options(fixture.Connection));
            Assert.Equal(0, await db.Tenants.CountAsync());
            Assert.Equal(0, await db.Memberships.CountAsync());
            Assert.Equal(0, await ReceiptCount(db));
            Assert.Equal(WorkspaceProvisioningOutcome.Created,
                (await Service(fixture, services, web.Clock).CreateAsync(principal, "Synthetic rollback", key, "workspace-rollback-check")).Outcome);
        });
    }

    [InfrastructureFact]
    public async Task Existing_tenant_id_used_as_request_key_cannot_assign_ownership_of_that_tenant()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var otherOwner = Guid.NewGuid();
            var existing = Tenant.Create(Guid.NewGuid(), "Existing synthetic", otherOwner);
            await using var db = new TenantsStoreContext(Options(fixture.Connection));
            db.Tenants.Add(existing);
            await db.SaveChangesAsync();
            var result = await Service(fixture, services, web.Clock).CreateAsync(principal, "New synthetic", existing.TenantId, "workspace-no-takeover");
            Assert.Equal(WorkspaceProvisioningOutcome.Created, result.Outcome);
            Assert.NotEqual(existing.TenantId, result.TenantId);
            var owner = await db.Memberships.AsNoTracking().SingleAsync(x => x.TenantId == existing.TenantId);
            Assert.Equal(otherOwner, owner.UserId);
            Assert.Equal(2, await db.Tenants.CountAsync());
        });
    }

    [InfrastructureFact]
    public async Task Request_keys_are_scoped_by_verified_actor_not_global_or_tenant_ids()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var key = Guid.NewGuid();
            var first = await Service(fixture, services, web.Clock).CreateAsync(principal, "Synthetic", key, "workspace-actor-check");
            // Storage-level test of the unique key. This is NOT an HTTP identity admission test.
            var second = await Writer(fixture, services).CreateAsync(Guid.NewGuid(), "Synthetic", key, "workspace-actor-check");
            Assert.Equal(WorkspaceProvisioningOutcome.Created, second.Outcome);
            Assert.NotEqual(first.TenantId, second.TenantId);
        });
    }

    [InfrastructureFact]
    public async Task Revoked_identity_cannot_replay_a_previously_successful_request()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var service = Service(fixture, services, web.Clock);
            var key = Guid.NewGuid();
            await service.CreateAsync(principal, "Synthetic", key, "workspace-revoked-check");
            Assert.True(await services.GetRequiredService<IAccountSessionValidator>().RevokeAllAsync(
                Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)));
            Assert.Equal(WorkspaceProvisioningOutcome.Unauthenticated,
                (await service.CreateAsync(principal, "Synthetic", key, "workspace-revoked-check")).Outcome);
        });
    }

    private static Task<int> ReceiptCount(TenantsStoreContext db) => db.Database.SqlQueryRaw<int>(
        "SELECT count(*)::integer AS \"Value\" FROM tenants.\"WorkspaceProvisioningRequests\"").SingleAsync();

    [InfrastructureFact]
    public async Task Migration_rollback_refuses_to_destroy_successful_idempotency_receipts()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            var key = Guid.NewGuid();
            var created = await Service(fixture, services, web.Clock).CreateAsync(principal, "Synthetic preserved", key, "workspace-migration-check");
            await using (var db = new TenantsStoreContext(Options(fixture.Connection)))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.GetService<IMigrator>()
                    .MigrateAsync("20261018075226_InitialTenants"));
                Assert.Equal("55000", Assert.IsType<PostgresException>(exception.InnerException).SqlState);
            }
            await using var check = new TenantsStoreContext(Options(fixture.Connection));
            Assert.Equal(1, await ReceiptCount(check));
            Assert.Equal(2, (await check.Database.GetAppliedMigrationsAsync()).Count());
            var replay = await Service(fixture, services, web.Clock).CreateAsync(principal, "Synthetic preserved", key, "workspace-migration-check");
            Assert.Equal(created.TenantId, replay.TenantId);
        });
    }

    [InfrastructureFact]
    public async Task Migration_upgrade_and_empty_receipt_rollback_preserve_existing_tenant_memberships_and_vector()
    {
        await WithIdentity(async (fixture, web, services, principal) =>
        {
            await using var db = new TenantsStoreContext(Options(fixture.Connection));
            var tenant = Tenant.Create(Guid.NewGuid(), "Synthetic pre-existing", Guid.NewGuid());
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync("20261018075226_InitialTenants");
            Assert.Equal(1, await db.Tenants.CountAsync());
            Assert.Equal(1, await db.Memberships.CountAsync());
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
            Assert.Equal(1, await db.Tenants.CountAsync());
            Assert.Equal(1, await db.Memberships.CountAsync());
            Assert.Equal(0, await ReceiptCount(db));
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::integer AS \"Value\" FROM pg_extension WHERE extname='vector'").SingleAsync());
        });
    }

    internal static async Task WithIdentity(Func<AccountSecurityFixture, WebSessionFixture, IServiceProvider, ClaimsPrincipal, Task> test)
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password,
                new AccountRequestContext(IPAddress.Loopback, "provision-setup"));
            var manager = setup.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
            var user = (await manager.FindByEmailAsync(fixture.Email))!;
            user.EmailConfirmed = true;
            Assert.True((await manager.UpdateAsync(user)).Succeeded);
        }
        await using (var db = new TenantsStoreContext(Options(fixture.Connection))) { await db.Database.MigrateAsync(); }
        using var web = new WebSessionFixture(fixture);
        var host = web.CreateHost();
        using var client = WebSessionFixture.Client(host);
        Assert.Equal(HttpStatusCode.NoContent, (await web.Login(client)).StatusCode);
        var begin = await WebSessionFixture.Write(client, "mfa/enrollment", new { password = fixture.Password });
        Assert.Equal(HttpStatusCode.OK, begin.StatusCode);
        var key = JsonSerializer.Deserialize<JsonElement>(await begin.Content.ReadAsStringAsync()).GetProperty("sharedKey").GetString()!;
        var confirm = await WebSessionFixture.Write(client, "mfa/confirm", new { password = fixture.Password, code = MfaTests.Totp(key) });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var recovery = JsonSerializer.Deserialize<JsonElement>(await confirm.Content.ReadAsStringAsync()).GetProperty("recoveryCodes")[0].GetString()!;
        await using var scope = host.Services.CreateAsyncScope();
        var login = await scope.ServiceProvider.GetRequiredService<IAccountLoginService>().CheckAsync(fixture.Email, fixture.Password,
            IPAddress.Loopback, "provision-login", recoveryCode: recovery);
        Assert.Equal(LoginOutcome.Accepted, login.Outcome);
        await test(fixture, web, scope.ServiceProvider, login.Principal!);
    }

    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Synthetic pre-commit failure.");
    }
}
