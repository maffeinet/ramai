namespace Ramai.Application.Contracts.Identity;

// Minimal identity eligibility; no email, password, hash, token or Identity entity.
public sealed record UserAccountInfo(Guid UserId, bool IsActive, bool IsEmailConfirmed)
{
    public bool CanAccessTenantWorkspaces => UserId != Guid.Empty && IsActive && IsEmailConfirmed;
}
