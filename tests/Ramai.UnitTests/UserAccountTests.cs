using Ramai.Application.Contracts.Identity;
using Ramai.Modules.Identity.Domain;

namespace Ramai.UnitTests;

public sealed class UserAccountTests
{
    [Fact]
    public void User_has_a_stable_global_id_and_starts_unconfirmed()
    {
        var id = Guid.NewGuid();
        var user = UserAccount.Create(id, "  Member@example.test  ");
        Assert.Equal(id, user.UserId);
        Assert.Equal("Member@example.test", user.Email);
        Assert.Equal("MEMBER@EXAMPLE.TEST", user.NormalizedEmail);
        Assert.True(user.IsActive);
        Assert.False(user.IsEmailConfirmed);
        Assert.False(user.CanAccessTenantWorkspaces);
    }

    [Fact]
    public void Empty_id_is_rejected() =>
        Assert.Throws<ArgumentException>(() => UserAccount.Create(Guid.Empty, "member@example.test"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("member@@example.test")]
    [InlineData("Display Name <member@example.test>")]
    public void Invalid_email_is_rejected(string? email) =>
        Assert.ThrowsAny<ArgumentException>(() => UserAccount.Create(Guid.NewGuid(), email!));

    [Fact]
    public void Confirmation_does_not_reactivate_a_disabled_account()
    {
        var user = UserAccount.Create(Guid.NewGuid(), "member@example.test");
        user.ConfirmEmail();
        Assert.True(user.CanAccessTenantWorkspaces);
        user.Deactivate();
        user.ConfirmEmail();
        Assert.True(user.IsEmailConfirmed);
        Assert.False(user.IsActive);
        Assert.False(user.CanAccessTenantWorkspaces);
    }

    [Fact]
    public void Email_normalization_is_case_insensitive_but_does_not_merge_aliases()
    {
        var first = UserAccount.Create(Guid.NewGuid(), "Member@example.test");
        var second = UserAccount.Create(Guid.NewGuid(), "member@EXAMPLE.test");
        var alias = UserAccount.Create(Guid.NewGuid(), "member+other@example.test");
        Assert.Equal(first.NormalizedEmail, second.NormalizedEmail);
        Assert.NotEqual(first.NormalizedEmail, alias.NormalizedEmail);
        Assert.NotEqual(first.UserId, second.UserId); // Storage must enforce global uniqueness later.
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Identity_contract_requires_both_active_and_confirmed(
        bool active, bool confirmed, bool eligible)
    {
        var info = new UserAccountInfo(Guid.NewGuid(), active, confirmed);
        Assert.Equal(eligible, info.CanAccessTenantWorkspaces);
    }

    [Fact]
    public void Identity_contract_with_empty_id_is_not_eligible()
    {
        var info = new UserAccountInfo(Guid.Empty, true, true);
        Assert.False(info.CanAccessTenantWorkspaces);
    }
}
