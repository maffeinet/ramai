using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ramai.Infrastructure.Connectivity;

namespace Ramai.Api.Health;

public sealed class PostgreSqlHealthCheck(IInfrastructureConnectivityVerifier verifier) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await verifier.CheckPostgreSqlAsync(cancellationToken);

        return result.IsAvailable
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy(
                "PostgreSQL is not reachable.",
                data: new Dictionary<string, object> { ["errorCode"] = result.ErrorCode ?? "unknown" });
    }
}
