using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Ramai.Application.Contracts.Tenants;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.IntegrationTests;

public sealed class PersistenceTests
{
    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Empty_database_migrates_and_round_trips_accounts_and_memberships()
    {
        await WithDatabase(async connection =>
        {
            await using var identity = Identity(connection);
            await using var tenants = Tenants(connection);
            await identity.Database.MigrateAsync();
            await tenants.Database.MigrateAsync();
            Assert.False(identity.Database.HasPendingModelChanges());
            Assert.False(tenants.Database.HasPendingModelChanges());
            var userId = Guid.NewGuid();
            identity.Users.Add(User(userId));
            await identity.SaveChangesAsync();
            var a = Tenant.Create(Guid.NewGuid(), "Synthetic A", userId);
            var b = Tenant.Create(Guid.NewGuid(), "Synthetic B", Guid.NewGuid());
            b.AddMember(userId);
            tenants.AddRange(a, b);
            await tenants.SaveChangesAsync();
            tenants.ChangeTracker.Clear();
            var loaded = await tenants.Tenants.Include(x => x.Memberships).SingleAsync(x => x.TenantId == a.TenantId);
            Assert.Single(loaded.Memberships);
            Assert.Equal(MembershipRole.Owner, loaded.FindActiveMembership(userId)!.Role);
            var reader = new TenantMembershipReader(tenants);
            Assert.Equal(TenantAccessRole.Owner, (await reader.FindActiveAsync(a.TenantId, userId))!.Role);
            Assert.Equal(TenantAccessRole.Member, (await reader.FindActiveAsync(b.TenantId, userId))!.Role);
            Assert.Null(await reader.FindActiveAsync(a.TenantId, Guid.NewGuid()));
            var account = await new UserAccountReader(identity).FindAsync(userId);
            Assert.NotNull(account);
            Assert.False(account.CanAccessTenantWorkspaces);
            Assert.Null(await new UserAccountReader(identity).FindAsync(Guid.NewGuid()));
            Assert.IsType<string>(await Scalar(connection, "SELECT extversion FROM pg_extension WHERE extname='vector'"));
        });
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Foundation_upgrade_is_idempotent_and_rollback_reapply_preserves_existing_data_and_vector()
    {
        await WithDatabase(async connection =>
        {
            await Execute(connection, "CREATE EXTENSION IF NOT EXISTS vector; CREATE TABLE public.foundation_probe (id integer PRIMARY KEY, value text NOT NULL); INSERT INTO public.foundation_probe VALUES (1, 'synthetic-preserved')");
            await using var identity = Identity(connection);
            await using var tenants = Tenants(connection);
            await identity.Database.MigrateAsync();
            await tenants.Database.MigrateAsync();
            await identity.Database.MigrateAsync();
            await tenants.Database.MigrateAsync();
            Assert.Single(await identity.Database.GetAppliedMigrationsAsync());
            Assert.Single(await tenants.Database.GetAppliedMigrationsAsync());
            // Destructive recovery is deliberately restricted to this disposable DB.
            await tenants.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
            await identity.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase);
            Assert.Equal("synthetic-preserved", await Scalar(connection, "SELECT value FROM public.foundation_probe WHERE id=1"));
            Assert.NotNull(await Scalar(connection, "SELECT extversion FROM pg_extension WHERE extname='vector'"));
            await identity.Database.MigrateAsync();
            await tenants.Database.MigrateAsync();
            Assert.Equal("synthetic-preserved", await Scalar(connection, "SELECT value FROM public.foundation_probe WHERE id=1"));
            Assert.False(identity.Database.HasPendingModelChanges());
            Assert.False(tenants.Database.HasPendingModelChanges());
        });
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Database_rejects_duplicate_normalized_email_primary_keys_and_empty_account_id()
    {
        await WithDatabase(async connection =>
        {
            await using var identity = Identity(connection);
            await identity.Database.MigrateAsync();
            var user = User(Guid.NewGuid());
            identity.Add(user);
            await identity.SaveChangesAsync();
            identity.ChangeTracker.Clear();
            var duplicate = User(Guid.NewGuid());
            duplicate.NormalizedEmail = user.NormalizedEmail;
            identity.Add(duplicate);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => identity.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            identity.ChangeTracker.Clear();
            identity.Add(User(user.Id));
            error = await Assert.ThrowsAsync<DbUpdateException>(() => identity.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
            identity.ChangeTracker.Clear();
            identity.Add(User(Guid.Empty));
            error = await Assert.ThrowsAsync<DbUpdateException>(() => identity.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        });
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Membership_constraints_and_transaction_rollback_are_enforced_by_postgresql()
    {
        await WithDatabase(async connection =>
        {
            await using var tenants = Tenants(connection);
            await tenants.Database.MigrateAsync();
            var owner = Guid.NewGuid();
            var member = Guid.NewGuid();
            var tenant = Tenant.Create(Guid.NewGuid(), "Synthetic", owner);
            tenant.AddMember(member);
            tenant.DeactivateMember(member);
            tenants.Add(tenant);
            await tenants.SaveChangesAsync();
            Assert.Null(await new TenantMembershipReader(tenants).FindActiveAsync(tenant.TenantId, member));
            await ExpectSqlState(connection, PostgresErrorCodes.UniqueViolation,
                "INSERT INTO tenants.\"Memberships\" (\"TenantId\", \"UserId\", \"Role\", \"Status\") VALUES ($1,$2,0,0)", tenant.TenantId, member);
            await ExpectSqlState(connection, PostgresErrorCodes.ForeignKeyViolation,
                "INSERT INTO tenants.\"Memberships\" (\"TenantId\", \"UserId\", \"Role\", \"Status\") VALUES ($1,$2,0,0)", Guid.NewGuid(), member);
            await ExpectSqlState(connection, PostgresErrorCodes.CheckViolation,
                "INSERT INTO tenants.\"Memberships\" (\"TenantId\", \"UserId\", \"Role\", \"Status\") VALUES ($1,$2,0,0)", tenant.TenantId, Guid.Empty);
            await ExpectSqlState(connection, PostgresErrorCodes.UniqueViolation,
                "INSERT INTO tenants.\"Memberships\" (\"TenantId\", \"UserId\", \"Role\", \"Status\") VALUES ($1,$2,2,0)", tenant.TenantId, Guid.NewGuid());
            await ExpectSqlState(connection, PostgresErrorCodes.CheckViolation,
                "UPDATE tenants.\"Memberships\" SET \"Role\"=99 WHERE \"TenantId\"=$1 AND \"UserId\"=$2", tenant.TenantId, member);
            await ExpectSqlState(connection, PostgresErrorCodes.CheckViolation,
                "UPDATE tenants.\"Memberships\" SET \"Status\"=99 WHERE \"TenantId\"=$1 AND \"UserId\"=$2", tenant.TenantId, member);
            await ExpectSqlState(connection, PostgresErrorCodes.CheckViolation,
                "UPDATE tenants.\"Memberships\" SET \"Status\"=1 WHERE \"TenantId\"=$1 AND \"UserId\"=$2", tenant.TenantId, owner);
            var rolledBackId = Guid.NewGuid();
            await using (var transaction = await tenants.Database.BeginTransactionAsync())
            {
                tenants.Add(Tenant.Create(rolledBackId, "Rolled back", owner));
                await tenants.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            tenants.ChangeTracker.Clear();
            Assert.False(await tenants.Tenants.AnyAsync(x => x.TenantId == rolledBackId));
            Assert.False(await tenants.Memberships.AnyAsync(x => x.TenantId == rolledBackId));
        });
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Concurrent_connections_cannot_create_duplicate_accounts_or_memberships()
    {
        await WithDatabase(async connection =>
        {
            await using var first = Identity(connection);
            await using var second = Identity(connection);
            await first.Database.MigrateAsync();
            var one = User(Guid.NewGuid());
            var two = User(Guid.NewGuid());
            two.NormalizedEmail = one.NormalizedEmail;
            first.Add(one);
            second.Add(two);
            var results = await Task.WhenAll(Record.ExceptionAsync(() => first.SaveChangesAsync()),
                Record.ExceptionAsync(() => second.SaveChangesAsync()));
            Assert.Single(results, result => result is null);
            var error = Assert.IsType<DbUpdateException>(Assert.Single(results, result => result is not null));
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);

            await using var tenants = Tenants(connection);
            await tenants.Database.MigrateAsync();
            var tenant = Tenant.Create(Guid.NewGuid(), "Concurrent", Guid.NewGuid());
            tenants.Add(tenant);
            await tenants.SaveChangesAsync();
            var member = Guid.NewGuid();
            async Task Insert()
            {
                await using var db = new NpgsqlConnection(connection);
                await db.OpenAsync();
                await using var command = new NpgsqlCommand("INSERT INTO tenants.\"Memberships\" (\"TenantId\", \"UserId\", \"Role\", \"Status\") VALUES ($1,$2,0,0)", db);
                command.Parameters.AddWithValue(tenant.TenantId);
                command.Parameters.AddWithValue(member);
                await command.ExecuteNonQueryAsync();
            }
            results = await Task.WhenAll(Record.ExceptionAsync(Insert), Record.ExceptionAsync(Insert));
            Assert.Single(results, result => result is null);
            Assert.Equal(PostgresErrorCodes.UniqueViolation,
                Assert.IsType<PostgresException>(Assert.Single(results, result => result is not null)).SqlState);
            Assert.Equal(1, await tenants.Memberships.CountAsync(x => x.TenantId == tenant.TenantId && x.UserId == member));
        });
    }

    private static AccountUser User(Guid id) => new()
    {
        Id = id,
        Email = $"{Guid.NewGuid():N}@example.invalid",
        NormalizedEmail = $"{Guid.NewGuid():N}@EXAMPLE.INVALID",
        UserName = $"synthetic-{Guid.NewGuid():N}",
        NormalizedUserName = $"SYNTHETIC-{Guid.NewGuid():N}"
    };

    private static IdentityStoreContext Identity(string connection) => new(new DbContextOptionsBuilder<IdentityStoreContext>()
        .UseNpgsql(connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "identity")).Options);

    private static TenantsStoreContext Tenants(string connection) => new(new DbContextOptionsBuilder<TenantsStoreContext>()
        .UseNpgsql(connection, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")).Options);

    private static async Task WithDatabase(Func<string, Task> test)
    {
        // Never migrate/reset the configured development database. Only create/drop a
        // random database owned by this invocation (same credentials as baseline CI).
        var database = "ramai_s1test_" + Guid.NewGuid().ToString("N");
        var options = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("RAMAI_DB_HOST") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RAMAI_DB_PORT") ?? "5432"),
            Username = Environment.GetEnvironmentVariable("RAMAI_DB_USER") ?? "ramai",
            Password = Environment.GetEnvironmentVariable("RAMAI_DB_PASSWORD")
                ?? throw new InvalidOperationException("Test database credentials are required."),
            Database = "postgres",
            Pooling = false,
            IncludeErrorDetail = false
        };
        await Execute(options.ConnectionString, $"CREATE DATABASE \"{database}\"");
        try
        {
            options.Database = database;
            await test(options.ConnectionString);
        }
        finally
        {
            options.Database = "postgres";
            await Execute(options.ConnectionString, $"DROP DATABASE \"{database}\" WITH (FORCE)");
        }
    }

    private static async Task Execute(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Scalar(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExpectSqlState(string connectionString, string state, string sql, Guid tenantId, Guid userId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(userId);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(state, exception.SqlState);
    }
}
