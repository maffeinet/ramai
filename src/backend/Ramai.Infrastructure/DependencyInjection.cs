using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ramai.Infrastructure.Configuration;
using Ramai.Infrastructure.Connectivity;

namespace Ramai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRamaiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new PostgreSqlOptions(
            GetValue(configuration, "RAMAI_DB_HOST", "localhost"),
            GetPort(configuration, "RAMAI_DB_PORT", 5432),
            GetValue(configuration, "RAMAI_DB_NAME", "ramai_dev"),
            GetValue(configuration, "RAMAI_DB_USER", "ramai"),
            configuration["RAMAI_DB_PASSWORD"] ?? string.Empty));

        services.AddSingleton(new RedisOptions(
            GetValue(configuration, "RAMAI_REDIS_HOST", "localhost"),
            GetPort(configuration, "RAMAI_REDIS_PORT", 6379)));

        services.AddSingleton<IInfrastructureConnectivityVerifier, InfrastructureConnectivityVerifier>();

        return services;
    }

    private static string GetValue(IConfiguration configuration, string key, string fallback) =>
        string.IsNullOrWhiteSpace(configuration[key]) ? fallback : configuration[key]!;

    private static int GetPort(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], out var port) ? port : fallback;
}
