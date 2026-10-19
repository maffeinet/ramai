using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ramai.Modules.Tenants.Persistence;

public sealed class TenantsStoreFactory : IDesignTimeDbContextFactory<TenantsStoreContext>
{
    public TenantsStoreContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("RAMAI_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=ramai_design;Username=ramai";
        return new TenantsStoreContext(new DbContextOptionsBuilder<TenantsStoreContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
            .Options);
    }
}
