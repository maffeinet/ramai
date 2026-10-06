namespace Ramai.Infrastructure.Connectivity;

public sealed record ConnectivityCheckResult(string Dependency, bool IsAvailable, string? ErrorCode = null);
