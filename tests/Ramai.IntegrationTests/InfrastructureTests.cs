using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Ramai.Infrastructure;
using Ramai.Infrastructure.Configuration;
using Ramai.Infrastructure.Connectivity;

namespace Ramai.IntegrationTests;

public sealed class InfrastructureTests
{
    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Real_services_are_reachable_and_vector_is_enabled()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        using var provider = new ServiceCollection().AddRamaiInfrastructure(configuration).BuildServiceProvider();
        var verifier = provider.GetRequiredService<IInfrastructureConnectivityVerifier>();
        Assert.True((await verifier.CheckPostgreSqlAsync()).IsAvailable, "PostgreSQL is not reachable.");
        Assert.True((await verifier.CheckRedisAsync()).IsAvailable, "Redis is not reachable.");
        var options = provider.GetRequiredService<PostgreSqlOptions>();
        await using var connection = new NpgsqlConnection(options.ToConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT extversion FROM pg_extension WHERE extname = 'vector'", connection);
        Assert.IsType<string>(await command.ExecuteScalarAsync());
    }
}

public sealed class InfrastructureFactAttribute : FactAttribute
{
    public InfrastructureFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RAMAI_RUN_INFRASTRUCTURE_TESTS") != "1")
        {
            Skip = "Set RAMAI_RUN_INFRASTRUCTURE_TESTS=1 and configure RAMAI_DB_* / RAMAI_REDIS_*.";
        }
    }
}
