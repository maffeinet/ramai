using Npgsql;
using Ramai.Infrastructure.Configuration;
using StackExchange.Redis;

namespace Ramai.Infrastructure.Connectivity;

internal sealed class InfrastructureConnectivityVerifier(
    PostgreSqlOptions postgreSqlOptions,
    RedisOptions redisOptions) : IInfrastructureConnectivityVerifier
{
    public async Task<ConnectivityCheckResult> CheckPostgreSqlAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(postgreSqlOptions.ToConnectionString());
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return new ConnectivityCheckResult("postgresql", true);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return new ConnectivityCheckResult("postgresql", false, exception.GetType().Name);
        }
    }

    public async Task<ConnectivityCheckResult> CheckRedisAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = 5_000,
                SyncTimeout = 5_000
            };
            configuration.EndPoints.Add(redisOptions.Host, redisOptions.Port);

            using var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
            await connection.GetDatabase().PingAsync();

            return new ConnectivityCheckResult("redis", true);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new ConnectivityCheckResult("redis", false, exception.GetType().Name);
        }
    }
}
