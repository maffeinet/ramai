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

public sealed class TenantIsolationTests
{
    private static DbContextOptions<TenantsStoreContext> BootstrapOptions(string connection) =>
        new DbContextOptionsBuilder<TenantsStoreContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")).Options;
    private static DbContextOptions<TenantDataContext> DataOptions(string connection) =>
        new DbContextOptionsBuilder<TenantDataContext>().UseNpgsql(connection).Options;

    private static async Task WithTenants(Func<AccountSecurityFixture, IServiceProvider, ClaimsPrincipal,
        Guid, Guid, Guid, TenantContext, TenantContext, Task> test)
    {
        await WorkspaceProvisioningIntegrationTests.WithIdentity(async (fixture, web, services, principal) =>
        {
            var owner = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var writer = new WorkspaceProvisioningStore(new PooledDbContextFactory<TenantsStoreContext>(BootstrapOptions(fixture.Connection)),
                services.GetRequiredService<ILogger<WorkspaceProvisioningStore>>());
            var a = (await writer.CreateAsync(owner, "Synthetic A", Guid.NewGuid(), "isolation-setup")).TenantId!.Value;
            var b = (await writer.CreateAsync(owner, "Synthetic B", Guid.NewGuid(), "isolation-setup")).TenantId!.Value;
            var member = Guid.NewGuid();
            await using var setup = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            var tenants = await setup.Tenants.Include(x => x.Memberships).ToArrayAsync();
            foreach (var tenant in tenants) { tenant.AddMember(member); }
            await setup.SaveChangesAsync();
            var resolver = new TenantContextResolver(services.GetRequiredService<IAccountSessionValidator>(),
                new TenantMembershipReader(setup), web.Clock);
            var contextA = (await resolver.SelectAsync(principal, a)).Context!;
            var contextB = (await resolver.SelectAsync(principal, b)).Context!;
            await test(fixture, services, principal, a, b, member, contextA, contextB);
        });
    }

    [InfrastructureFact]
    public async Task Lists_ids_find_navigations_and_no_tracking_never_return_other_tenant()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            Assert.Equal(a, (await db.Tenants.Include(x => x.Memberships).SingleAsync()).TenantId);
            Assert.All(await db.Memberships.AsNoTracking().ToArrayAsync(), x => Assert.Equal(a, x.TenantId));
            Assert.Empty(await db.Tenants.Where(x => x.TenantId == b).ToArrayAsync());
            Assert.Null(await db.Tenants.FindAsync(b));
            Assert.Null(await db.Memberships.FindAsync(b, member));
            Assert.NotNull(await db.Memberships.FindAsync(a, member));
            Assert.Equal(2, await db.Memberships.CountAsync());
            // The internal receipt is mapped and filtered too, without exposing it in a public DbSet.
            var type = db.Model.GetEntityTypes().Single(x => x.GetTableName() == "WorkspaceProvisioningRequests").ClrType;
            var setMethod = typeof(DbContext).GetMethods().Single(x => x.Name == "Set" && x.IsGenericMethod && x.GetParameters().Length == 0);
            var query = (IQueryable)setMethod.MakeGenericMethod(type).Invoke(db, null)!;
            var receipts = await query.Cast<object>().ToArrayAsync();
            Assert.Single(receipts);
            Assert.Equal(a, (Guid)type.GetProperty("TenantId")!.GetValue(receipts[0])!);
        });
    }

    [InfrastructureFact]
    public async Task Own_updates_and_deletes_are_persisted_without_touching_same_user_in_B()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using (var db = new TenantDataContext(DataOptions(fixture.Connection), ca))
            {
                var tenant = await db.Tenants.Include(x => x.Memberships).SingleAsync();
                db.Entry(tenant).Property(x => x.Name).CurrentValue = "Synthetic A updated";
                tenant.DeactivateMember(member);
                await db.SaveChangesAsync();
                db.Memberships.Remove((await db.Memberships.FindAsync(a, member))!);
                db.SaveChanges();
            }
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Synthetic A updated", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
            Assert.Equal(MembershipStatus.Active, (await check.Memberships.SingleAsync(x => x.TenantId == b && x.UserId == member)).Status);
            Assert.False(await check.Memberships.AnyAsync(x => x.TenantId == a && x.UserId == member));
        });
    }

    [InfrastructureTheory]
    [InlineData("add")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("attach-find")]
    public async Task Foreign_detached_entities_are_rejected_before_they_can_be_used(string operation)
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            var stub = Tenant.Create(b, "Client supplied", Guid.NewGuid());
            Assert.Throws<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case "add": db.Tenants.Add(stub); break;
                    case "update": db.Tenants.Update(stub); break;
                    case "delete": db.Tenants.Remove(stub); break;
                    default: db.Tenants.Attach(stub); break;
                }
            });
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
            Assert.Equal(2, await check.Tenants.CountAsync());
        });
    }

    [InfrastructureFact]
    public async Task Original_tenant_mismatch_rejects_even_when_current_entity_is_in_A()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            var tenant = await db.Tenants.SingleAsync();
            db.Entry(tenant).Property(x => x.Name).CurrentValue = "Should not persist";
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                // EF rejects key changes at assignment, before our SaveChanges guard.
                db.Entry(tenant).Property(x => x.TenantId).OriginalValue = b;
                await db.SaveChangesAsync();
            });
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal("Synthetic A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Own_insert_succeeds_and_mixed_batch_is_rejected_without_partial_writes()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            var newMember = Guid.NewGuid();
            await using (var db = new TenantDataContext(DataOptions(fixture.Connection), ca))
            {
                var tenant = await db.Tenants.Include(x => x.Memberships).SingleAsync();
                tenant.AddMember(newMember);
                await db.SaveChangesAsync();
            }
            await using (var db = new TenantDataContext(DataOptions(fixture.Connection), ca))
            {
                var own = await db.Tenants.SingleAsync();
                db.Entry(own).Property(x => x.Name).CurrentValue = "Should not persist";
                Assert.Throws<InvalidOperationException>(() => db.Tenants.Add(Tenant.Create(b, "Foreign", Guid.NewGuid())));
                // Even if application code catches the tracking error, no partial batch may commit.
                await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            }
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.True(await check.Memberships.AnyAsync(x => x.TenantId == a && x.UserId == newMember));
            Assert.False(await check.Memberships.AnyAsync(x => x.TenantId == b && x.UserId == newMember));
            Assert.Equal("Synthetic A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Changing_current_tenant_key_is_rejected_and_database_is_unchanged()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            var own = await db.Tenants.SingleAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                db.Entry(own).Property(x => x.TenantId).CurrentValue = b;
                await db.SaveChangesAsync();
            });
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal(2, await check.Tenants.CountAsync());
            Assert.Equal("Synthetic A", (await check.Tenants.SingleAsync(x => x.TenantId == a)).Name);
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Provisioning_receipts_cannot_be_updated_or_deleted_in_operational_context()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            var type = db.Model.GetEntityTypes().Single(x => x.GetTableName() == "WorkspaceProvisioningRequests").ClrType;
            var setMethod = typeof(DbContext).GetMethods().Single(x => x.Name == "Set" && x.IsGenericMethod && x.GetParameters().Length == 0);
            var query = (IQueryable)setMethod.MakeGenericMethod(type).Invoke(db, null)!;
            var receipt = Assert.Single(await query.Cast<object>().ToArrayAsync());
            db.Entry(receipt).Property("RequestName").CurrentValue = "Should not persist";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.Entry(receipt).State = EntityState.Deleted;
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        });
    }

    [InfrastructureTheory]
    [InlineData("ignore")]
    [InlineData("named-ignore")]
    [InlineData("compiled-ignore")]
    [InlineData("raw-query")]
    [InlineData("raw-scalar")]
    [InlineData("raw-write")]
    [InlineData("bulk-update")]
    [InlineData("bulk-delete")]
    public async Task Filter_disabling_raw_sql_and_bulk_paths_are_explicitly_rejected(string path)
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await using var db = new TenantDataContext(DataOptions(fixture.Connection), ca);
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                switch (path)
                {
                    case "ignore": await db.Tenants.IgnoreQueryFilters().CountAsync(); break;
                    case "named-ignore": await db.Tenants.IgnoreQueryFilters(["TenantIsolation"]).CountAsync(); break;
                    case "compiled-ignore":
                        var query = EF.CompileQuery((TenantDataContext context) => context.Tenants.IgnoreQueryFilters().Count());
                        query(db); break;
                    case "raw-query": await db.Tenants.FromSqlRaw("SELECT * FROM tenants.\"Tenants\"").CountAsync(); break;
                    case "raw-scalar": await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\"").SingleAsync(); break;
                    case "raw-write": await db.Database.ExecuteSqlRawAsync("UPDATE tenants.\"Tenants\" SET \"Name\" = 'Should not persist'"); break;
                    case "bulk-update": await db.Tenants.ExecuteUpdateAsync(x => x.SetProperty(y => y.Name, "Should not persist")); break;
                    case "bulk-delete": await db.Memberships.ExecuteDeleteAsync(); break;
                }
            });
            await using var check = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            Assert.Equal(2, await check.Tenants.CountAsync());
            Assert.Equal(4, await check.Memberships.CountAsync());
            Assert.Equal("Synthetic B", (await check.Tenants.SingleAsync(x => x.TenantId == b)).Name);
        });
    }

    [InfrastructureFact]
    public async Task Model_cache_and_concurrent_contexts_keep_distinct_tenant_parameters()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
            {
                var context = index % 2 == 0 ? ca : cb;
                await using var db = new TenantDataContext(DataOptions(fixture.Connection), context);
                Assert.Equal(context.TenantId, (await db.Tenants.SingleAsync()).TenantId);
                Assert.All(await db.Memberships.AsNoTracking().ToArrayAsync(), x => Assert.Equal(context.TenantId, x.TenantId));
            }));
        });
    }

    [InfrastructureFact]
    public async Task Missing_selected_context_or_revoked_session_cannot_create_operational_context()
    {
        await WithTenants(async (fixture, services, principal, a, b, member, ca, cb) =>
        {
            Assert.Throws<UnauthorizedAccessException>(() => new TenantDataContext(DataOptions(fixture.Connection), null!));
            await using var bootstrap = new TenantsStoreContext(BootstrapOptions(fixture.Connection));
            var resolver = new TenantContextResolver(services.GetRequiredService<IAccountSessionValidator>(),
                new TenantMembershipReader(bootstrap), TimeProvider.System);
            var factory = new TenantDataContextFactory(DataOptions(fixture.Connection), resolver);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.CreateAsync(principal));
            var selected = (await resolver.SelectAsync(principal, a)).SelectedPrincipal!;
            await using (var allowed = await factory.CreateAsync(selected))
            { Assert.Equal(a, (await allowed.Tenants.SingleAsync()).TenantId); }
            Assert.True(await services.GetRequiredService<IAccountSessionValidator>().RevokeAllAsync(ca.UserId));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => factory.CreateAsync(selected));
        });
    }
}
