namespace Ramai.Application.Contracts.Tenants;

public interface ITenantMembershipReader
{
    // The application must derive/validate these IDs from authenticated context.
    // Implementations return null for an absent/inactive membership; no global fallback.
    Task<TenantMembershipInfo?> FindActiveAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}
