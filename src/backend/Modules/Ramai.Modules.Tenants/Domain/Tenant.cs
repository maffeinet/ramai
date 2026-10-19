namespace Ramai.Modules.Tenants.Domain;

public sealed class Tenant
{
    private readonly List<Membership> _memberships = [];

    private Tenant(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
        Memberships = _memberships.AsReadOnly();
    }

    private Tenant(Guid tenantId, string name, Guid ownerUserId) : this(tenantId, name)
    {
        _memberships.Add(new Membership(tenantId, ownerUserId, MembershipRole.Owner));
    }

    public Guid TenantId { get; }
    public string Name { get; }
    public IReadOnlyList<Membership> Memberships { get; }

    // Identity eligibility and the authenticated creator are checked by the future
    // provisioning application service, not inferred from a client-supplied user ID.
    public static Tenant Create(Guid tenantId, string name, Guid ownerUserId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID must not be empty.", nameof(tenantId));
        }

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner user ID must not be empty.", nameof(ownerUserId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Tenant(tenantId, name.Trim(), ownerUserId);
    }

    public Membership AddMember(Guid userId, MembershipRole role = MembershipRole.Member)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        if (role == MembershipRole.Owner)
        {
            throw new InvalidOperationException("Owner management is outside this model's initial operations.");
        }

        if (_memberships.Any(membership => membership.UserId == userId))
        {
            throw new InvalidOperationException("Membership already exists for this user and tenant.");
        }

        var membership = new Membership(TenantId, userId, role);
        _memberships.Add(membership);
        return membership;
    }

    public Membership? FindActiveMembership(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        return _memberships.SingleOrDefault(membership =>
            membership.UserId == userId && membership.Status == MembershipStatus.Active);
    }

    public void DeactivateMember(Guid userId)
    {
        var membership = FindActiveMembership(userId)
            ?? throw new InvalidOperationException("Active membership not found in this tenant.");
        if (membership.Role == MembershipRole.Owner)
        {
            throw new InvalidOperationException("The initial owner cannot be deactivated by this operation.");
        }

        membership.Deactivate();
    }
}
