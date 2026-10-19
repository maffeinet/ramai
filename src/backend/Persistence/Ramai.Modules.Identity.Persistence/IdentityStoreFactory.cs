using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ramai.Modules.Identity.Persistence;

public sealed class IdentityStoreFactory : IDesignTimeDbContextFactory<IdentityStoreContext>
{
    public IdentityStoreContext CreateDbContext(string[] args)
    {
        // Scaffolding/scripts need no live credentials. Supply the connection for
        // database commands through process environment, not shell arguments/logs.
        var connection = Environment.GetEnvironmentVariable("RAMAI_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=ramai_design;Username=ramai";
        return new IdentityStoreContext(new DbContextOptionsBuilder<IdentityStoreContext>()
            .UseNpgsql(connection, provider => provider.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options);
    }
}
