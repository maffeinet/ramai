using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class WorkspaceOperationsIntegrationTests
{
    private static DbContextOptions<TenantsStoreContext> BootstrapOptions(string connection) =>
        new DbContextOptionsBuilder<TenantsStoreContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")).Options;
    private static WorkspaceDataStore Store(string connection) =>
        new(new DbContextOptionsBuilder<TenantDataContext>().UseNpgsql(connection).Options);

    // Auth/session/MFA originate from the existing real services and HTTP enrollment,
    // not a hand-crafted authenticated principal. Bootstrap writes below are synthetic test setup only.
    private static async Task WithWorkspaces(MembershipRole roleInB, Func<AccountSecurityFixture, IServiceProvider,
        ClaimsPrincipal, Guid, Guid, TenantContextResolver, WorkspaceOperationsService, Task> test)
    {
        await WorkspaceProvisioningIntegrationTests.WithIdentity(async (fixture, web, services, principal) =>
        {
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var writer = new WorkspaceProvisioningStore(new PooledDbContextFactory<TenantsStoreContext>(BootstrapOptions(fixture.Connection)),
                services.GetRequiredService<ILogger<WorkspaceProvisioningStore>>());
            var a = (await writer.CreateAsync(userId, "Synthetic A", Guid.NewGuid(), "workspace-rbac-setup")).TenantId!.Value;
            var b = (await writer.CreateAsync(Guid.NewGuid(), "Synthetic B", Guid.NewGuid(), "workspace-rbac-setup")).TenantId!.Value;
            await using var db = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            var other = await db.Tenants.Include(x => x.Memberships).SingleAsync(x => x.TenantId == b);
            other.AddMember(userId, roleInB);
            await db.SaveChangesAsync();
            var resolver = new TenantContextResolver(services.GetRequiredService<IAccountSessionValidator>(),
                new TenantMembershipReader(db), web.Clock);
            await test(fixture, services, principal, a, b, resolver, new(resolver, Store(fixture.Connection)));
        });
    }

    [InfrastructureFact]
    public async Task Owner_A_cannot_use_that_role_in_B_and_rename_is_scoped_to_A()
    {
        await WithWorkspaces(MembershipRole.Member, async (fixture, services, principal, a, b, resolver, operations) =>
        {
            var selectedA = (await resolver.SelectAsync(principal, a)).SelectedPrincipal!;
            var selectedB = (await resolver.SelectAsync(principal, b)).SelectedPrincipal!;
            Assert.Equal(WorkspaceOperationOutcome.Accepted, (await operations.RenameAsync(selectedA, " Renamed A ")).Outcome);
            Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await operations.RenameAsync(selectedB, "Denied B")).Outcome);
            Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await operations.ReadSettingsAsync(selectedB)).Outcome);
            var readB = await operations.ReadAsync(selectedB);
            Assert.Equal(WorkspaceOperationOutcome.Accepted, readB.Outcome);
            Assert.Equal(TenantAccessRole.Member, readB.Workspace!.Role);
            Assert.Equal("Synthetic B", readB.Workspace.Name);
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Renamed A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }

    [InfrastructureTheory]
    [InlineData(MembershipRole.Admin, WorkspaceOperationOutcome.Accepted)]
    [InlineData(MembershipRole.Member, WorkspaceOperationOutcome.Forbidden)]
    public async Task Settings_permissions_are_enforced_with_real_membership(MembershipRole role, WorkspaceOperationOutcome expected)
    {
        await WithWorkspaces(role, async (fixture, services, principal, a, b, resolver, operations) =>
        {
            var selected = (await resolver.SelectAsync(principal, b)).SelectedPrincipal!;
            Assert.Equal(expected, (await operations.ReadSettingsAsync(selected)).Outcome);
            Assert.Equal(expected, (await operations.RenameAsync(selected, "Renamed B")).Outcome);
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal(expected == WorkspaceOperationOutcome.Accepted ? "Renamed B" : "Synthetic B",
                (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
            Assert.Equal("Synthetic A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Membership_deactivation_and_role_downgrade_take_effect_on_next_operation()
    {
        await WithWorkspaces(MembershipRole.Admin, async (fixture, services, principal, a, b, resolver, operations) =>
        {
            var selected = (await resolver.SelectAsync(principal, b)).SelectedPrincipal!;
            Assert.Equal(WorkspaceOperationOutcome.Accepted, (await operations.RenameAsync(selected, "Admitted B")).Outcome);
            var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using (var setup = new TenantsStoreContext(BootstrapOptions(fixture.Connection)))
            {
                var membership = await setup.Memberships.SingleAsync(x => x.TenantId == b && x.UserId == userId);
                setup.Entry(membership).Property(x => x.Role).CurrentValue = MembershipRole.Member;
                await setup.SaveChangesAsync();
            }
            Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await operations.RenameAsync(selected, "Denied")).Outcome);
            Assert.Equal(TenantAccessRole.Member, (await operations.ReadAsync(selected)).Workspace!.Role);
            await using (var setup = new TenantsStoreContext(BootstrapOptions(fixture.Connection)))
            {
                var tenant = await setup.Tenants.Include(x => x.Memberships).SingleAsync(x => x.TenantId == b);
                tenant.DeactivateMember(userId);
                await setup.SaveChangesAsync();
            }
            Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await operations.ReadAsync(selected)).Outcome);
            Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await operations.RenameAsync(selected, "Still denied")).Outcome);
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Admitted B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Account_revocation_denies_operations_and_missing_selection_never_uses_default()
    {
        await WithWorkspaces(MembershipRole.Member, async (fixture, services, principal, a, b, resolver, operations) =>
        {
            Assert.Equal(WorkspaceOperationOutcome.SelectionRequired, (await operations.ReadAsync(principal)).Outcome);
            var selected = (await resolver.SelectAsync(principal, a)).SelectedPrincipal!;
            Assert.Equal(WorkspaceOperationOutcome.Accepted, (await operations.ReadAsync(selected)).Outcome);
            Assert.True(await services.GetRequiredService<IAccountSessionValidator>()
                .RevokeAllAsync(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)));
            Assert.Equal(WorkspaceOperationOutcome.Unauthenticated, (await operations.ReadAsync(selected)).Outcome);
            Assert.Equal(WorkspaceOperationOutcome.Unauthenticated, (await operations.RenameAsync(selected, "Denied")).Outcome);
        });
    }

    [InfrastructureFact]
    public async Task Direct_adapter_write_with_member_context_is_rejected_and_names_are_bounded()
    {
        await WithWorkspaces(MembershipRole.Member, async (fixture, services, principal, a, b, resolver, operations) =>
        {
            var contextB = (await resolver.SelectAsync(principal, b)).Context!;
            var store = Store(fixture.Connection);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.RenameAsync(contextB, "Denied"));
            var owner = (await resolver.SelectAsync(principal, a)).Context!;
            await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync(owner, "Invalid\nname"));
            await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync(owner, new string('x', 257)));
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Synthetic A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }
}
