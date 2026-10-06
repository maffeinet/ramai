namespace Ramai.Infrastructure.Connectivity;

public interface IInfrastructureConnectivityVerifier
{
    Task<ConnectivityCheckResult> CheckPostgreSqlAsync(CancellationToken cancellationToken = default);

    Task<ConnectivityCheckResult> CheckRedisAsync(CancellationToken cancellationToken = default);
}
