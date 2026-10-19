using Ramai.Modules.Tenants.Domain;

namespace Ramai.UnitTests;

public sealed class TenantTests
{
    [Fact]
    public void Creation_establishes_exactly_one_active_owner_in_the_same_tenant()
    {
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var tenant = Tenant.Create(tenantId, "  Example company  ", ownerId);
        Assert.Equal(tenantId, tenant.TenantId);
        Assert.Equal("Example company", tenant.Name);
        var owner = Assert.Single(tenant.Memberships);
        Assert.Equal(tenantId, owner.TenantId);
        Assert.Equal(ownerId, owner.UserId);
        Assert.Equal(MembershipRole.Owner, owner.Role);
        Assert.Equal(MembershipStatus.Active, owner.Status);
    }

    [Fact]
    public void Empty_tenant_and_owner_ids_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.Empty, "Company", Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), "Company", Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_name_is_rejected(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => Tenant.Create(Guid.NewGuid(), name!, Guid.NewGuid()));

    [Fact]
    public void New_members_are_scoped_and_default_to_member()
    {
        var tenant = CreateTenant();
        var userId = Guid.NewGuid();
        var membership = tenant.AddMember(userId);
        Assert.Equal(tenant.TenantId, membership.TenantId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(MembershipRole.Member, membership.Role);
        Assert.Same(membership, tenant.FindActiveMembership(userId));
    }

    [Fact]
    public void Same_user_can_be_owner_in_one_tenant_and_member_in_another()
    {
        var userId = Guid.NewGuid();
        var first = Tenant.Create(Guid.NewGuid(), "A", userId);
        var second = CreateTenant();
        second.AddMember(userId);
        Assert.Equal(MembershipRole.Owner, first.FindActiveMembership(userId)!.Role);
        Assert.Equal(MembershipRole.Member, second.FindActiveMembership(userId)!.Role);
        Assert.NotEqual(first.TenantId, second.FindActiveMembership(userId)!.TenantId);
        Assert.Null(first.FindActiveMembership(second.Memberships.Single(m => m.Role == MembershipRole.Owner).UserId));
    }

    [Fact]
    public void Admin_role_does_not_create_membership_in_another_tenant()
    {
        var first = CreateTenant();
        var second = CreateTenant();
        var userId = Guid.NewGuid();
        first.AddMember(userId, MembershipRole.Admin);
        Assert.Equal(MembershipRole.Admin, first.FindActiveMembership(userId)!.Role);
        Assert.Null(second.FindActiveMembership(userId));
    }

    [Fact]
    public void Duplicate_membership_is_rejected_even_after_deactivation()
    {
        var tenant = CreateTenant();
        var userId = Guid.NewGuid();
        tenant.AddMember(userId);
        Assert.Throws<InvalidOperationException>(() => tenant.AddMember(userId, MembershipRole.Admin));
        tenant.DeactivateMember(userId);
        Assert.Null(tenant.FindActiveMembership(userId));
        Assert.Equal(MembershipStatus.Inactive, tenant.Memberships.Single(m => m.UserId == userId).Status);
        Assert.Throws<InvalidOperationException>(() => tenant.AddMember(userId));
        Assert.Equal(2, tenant.Memberships.Count);
    }

    [Fact]
    public void Duplicate_owner_membership_is_rejected()
    {
        var tenant = CreateTenant();
        Assert.Throws<InvalidOperationException>(() => tenant.AddMember(tenant.Memberships[0].UserId));
    }

    [Fact]
    public void Owner_management_is_not_exposed_by_member_operations()
    {
        var tenant = CreateTenant();
        Assert.Throws<InvalidOperationException>(() => tenant.AddMember(Guid.NewGuid(), MembershipRole.Owner));
        Assert.Throws<InvalidOperationException>(() => tenant.DeactivateMember(tenant.Memberships[0].UserId));
        Assert.Equal(MembershipStatus.Active, Assert.Single(tenant.Memberships).Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Undefined_role_is_rejected(int role)
    {
        var tenant = CreateTenant();
        Assert.Throws<ArgumentOutOfRangeException>(() => tenant.AddMember(Guid.NewGuid(), (MembershipRole)role));
        Assert.Single(tenant.Memberships);
    }

    [Fact]
    public void Empty_member_id_is_rejected_by_all_operations()
    {
        var tenant = CreateTenant();
        Assert.Throws<ArgumentException>(() => tenant.AddMember(Guid.Empty));
        Assert.Throws<ArgumentException>(() => tenant.FindActiveMembership(Guid.Empty));
        Assert.Throws<ArgumentException>(() => tenant.DeactivateMember(Guid.Empty));
    }

    [Fact]
    public void Unknown_user_cannot_be_deactivated_and_has_no_membership()
    {
        var tenant = CreateTenant();
        var userId = Guid.NewGuid();
        Assert.Null(tenant.FindActiveMembership(userId));
        Assert.Throws<InvalidOperationException>(() => tenant.DeactivateMember(userId));
    }

    [Fact]
    public void Membership_collection_cannot_be_mutated_by_callers()
    {
        var tenant = CreateTenant();
        var collection = (ICollection<Membership>)tenant.Memberships;
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Single(tenant.Memberships);
    }

    [Fact]
    public void Default_role_is_member_not_owner()
    {
        Assert.Equal(MembershipRole.Member, default);
        Assert.Equal(Ramai.Application.Contracts.Tenants.TenantAccessRole.Member, default);
    }

    private static Tenant CreateTenant() => Tenant.Create(Guid.NewGuid(), "Company", Guid.NewGuid());
}
