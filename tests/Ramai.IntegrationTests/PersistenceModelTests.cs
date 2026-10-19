using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Contexts_map_only_tables_and_foreign_keys_owned_by_their_module()
    {
        using var identity = new IdentityStoreFactory().CreateDbContext([]);
        using var tenants = new TenantsStoreFactory().CreateDbContext([]);
        AssertOwnership(identity, "identity", ["Users", "UserClaims", "UserLogins", "UserTokens"]);
        AssertOwnership(tenants, "tenants", ["Tenants", "Memberships"]);
    }

    [Fact]
    public void Initial_scripts_do_not_drop_vector_and_histories_are_separate()
    {
        using var identity = new IdentityStoreFactory().CreateDbContext([]);
        using var tenants = new TenantsStoreFactory().CreateDbContext([]);
        var identityScript = identity.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript();
        var tenantsScript = tenants.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().GenerateScript();
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS vector", identityScript);
        Assert.Contains("identity.\"__EFMigrationsHistory\"", identityScript);
        Assert.Contains("tenants.\"__EFMigrationsHistory\"", tenantsScript);
        Assert.DoesNotContain("DROP EXTENSION", identityScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("identity.", tenantsScript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tenants.", identityScript, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOwnership(DbContext context, string schema, string[] expectedTables)
    {
        var entities = context.Model.GetEntityTypes().ToArray();
        Assert.Equal(expectedTables.Order(), entities.Select(entity => entity.GetTableName()).Order());
        foreach (var entity in entities)
        {
            Assert.Equal(schema, entity.GetSchema());
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                Assert.Equal(schema, foreignKey.PrincipalEntityType.GetSchema());
            }
        }
    }
}
