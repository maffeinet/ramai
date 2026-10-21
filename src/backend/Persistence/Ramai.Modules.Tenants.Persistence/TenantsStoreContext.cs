using Microsoft.EntityFrameworkCore;
using Ramai.Modules.Tenants.Domain;

namespace Ramai.Modules.Tenants.Persistence;

// Schema/bootstrap only. Runtime operations must use TenantDataContext.
// Reviewed consumers: design-time migrations, exact-pair membership reader, verified provisioning writer.
public sealed class TenantsStoreContext(DbContextOptions<TenantsStoreContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Membership> Memberships => Set<Membership>();
    protected override void OnModelCreating(ModelBuilder builder) => TenantsModel.Configure(builder);
}
