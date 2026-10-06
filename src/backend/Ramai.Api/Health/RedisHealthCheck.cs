using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ramai.Infrastructure.Connectivity;

namespace Ramai.Api.Health;

public sealed class RedisHealthCheck(IInfrastructureConnectivityVerifier verifier) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await verifier.CheckRedisAsync(cancellationToken);

        return result.IsAvailable
            ? HealthCheckResult.Healthy("Redis is reachable.")
            : HealthCheckResult.Unhealthy(
                "Redis is not reachable.",
                data: new Dictionary<string, object> { ["errorCode"] = result.ErrorCode ?? "unknown" });
    }
}
