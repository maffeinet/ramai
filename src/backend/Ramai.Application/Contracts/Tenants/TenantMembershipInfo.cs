namespace Ramai.Application.Contracts.Tenants;

// A read-only active-membership snapshot, not a session or long-lived permission cache.
public sealed record TenantMembershipInfo(Guid TenantId, Guid UserId, TenantAccessRole Role);
