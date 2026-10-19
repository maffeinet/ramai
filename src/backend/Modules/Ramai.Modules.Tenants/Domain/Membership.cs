namespace Ramai.Modules.Tenants.Domain;

public sealed class Membership
{
    internal Membership(Guid tenantId, Guid userId, MembershipRole role)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID must not be empty.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        TenantId = tenantId;
        UserId = userId;
        Role = role;
    }

    public Guid TenantId { get; }
    public Guid UserId { get; }
    public MembershipRole Role { get; }
    public MembershipStatus Status { get; private set; } = MembershipStatus.Active;

    internal void Deactivate() => Status = MembershipStatus.Inactive;
}
