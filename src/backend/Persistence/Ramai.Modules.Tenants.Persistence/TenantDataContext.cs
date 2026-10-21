using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Tenants;
using Ramai.Modules.Tenants.Domain;

namespace Ramai.Modules.Tenants.Persistence;

// One immutable verified tenant per operation. No Guid/string tenant constructor, mutable setter or pooling.
public sealed class TenantDataContext : DbContext
{
    public Guid ActiveTenantId { get; }
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();

    public TenantDataContext(DbContextOptions<TenantDataContext> options, TenantContext verifiedContext) : base(options)
    {
        if (verifiedContext is null || verifiedContext.TenantId == Guid.Empty || verifiedContext.UserId == Guid.Empty)
        { throw new UnauthorizedAccessException("A verified tenant context is required."); }
        ActiveTenantId = verifiedContext.TenantId;
        ChangeTracker.Tracked += (_, args) => CheckTenant(args.Entry);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(TenantQueryBoundary.Instance, TenantCommandBoundary.Instance);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        TenantsModel.Configure(builder);
        builder.Entity<Tenant>().HasQueryFilter(x => x.TenantId == ActiveTenantId);
        builder.Entity<Membership>().HasQueryFilter(x => x.TenantId == ActiveTenantId);
        builder.Entity<WorkspaceProvisioningRequest>().HasQueryFilter(x => x.TenantId == ActiveTenantId);
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            entity.FindProperty("TenantId")!.IsConcurrencyToken = true;
            entity.SetIsTableExcludedFromMigrations(true);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void CheckTenant(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var property = entry.Metadata.FindProperty("TenantId")
            ?? throw new InvalidOperationException("Unscoped entity is not permitted in tenant operations.");
        if (entry.Property(property.Name).CurrentValue is not Guid id || id != ActiveTenantId)
        { throw new InvalidOperationException("Tenant isolation boundary rejected the operation."); }
    }

    private void GuardWrites()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            CheckTenant(entry);
            if (entry.Entity is WorkspaceProvisioningRequest)
            { throw new InvalidOperationException("Provisioning receipts are immutable in tenant operations."); }
            if (entry.State != EntityState.Added && (entry.Property("TenantId").OriginalValue is not Guid original
                || original != ActiveTenantId || entry.Property("TenantId").IsModified))
            { throw new InvalidOperationException("Tenant isolation boundary rejected the operation."); }
        }
    }
}
