using System.Security.Claims;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;

namespace Ramai.UnitTests;

public sealed class WorkspaceProvisioningTests
{
    private static ClaimsPrincipal Session(Guid user, bool mfa = true) => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(AccountSessionClaims.SecurityStamp, "synthetic-marker"),
        new Claim(AccountSessionClaims.Started, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
        new Claim(AccountSessionClaims.Mfa, mfa ? "true" : "false")
    ], AccountSessionClaims.Scheme));

    [Fact]
    public async Task Verified_subject_becomes_owner_without_selected_tenant_or_client_role()
    {
        var user = Guid.NewGuid();
        var request = Guid.NewGuid();
        var store = new Store();
        var service = Service(user, store);
        var principal = Session(user);
        var result = await service.CreateAsync(principal, "  Synthetic workspace  ", request, "provision-check");
        Assert.Equal(WorkspaceProvisioningOutcome.Created, result.Outcome);
        Assert.Equal(user, store.Owner);
        Assert.Equal(request, store.Request);
        Assert.Equal("Synthetic workspace", store.Name);
        Assert.Equal("provision-check", store.Correlation);
        Assert.Null(principal.FindFirst(TenantSessionClaims.SelectedTenant));
        Assert.Null(principal.FindFirst(ClaimTypes.Role));
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("duplicate-user")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("missing-account")]
    [InlineData("inactive")]
    [InlineData("unconfirmed")]
    [InlineData("wrong-account")]
    public async Task Invalid_account_or_session_never_reaches_writer(string failure)
    {
        var user = Guid.NewGuid();
        var principal = Session(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        if (failure == "anonymous") { principal = new(new ClaimsIdentity()); }
        if (failure == "duplicate-user") { identity.AddClaim(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())); }
        if (failure == "expired")
        {
            identity.RemoveClaim(identity.FindFirst(AccountSessionClaims.Started));
            identity.AddClaim(new(AccountSessionClaims.Started, "0"));
        }
        var store = new Store();
        var account = failure == "missing-account" ? null : new UserAccountInfo(
            failure == "wrong-account" ? Guid.NewGuid() : user, failure != "inactive", failure != "unconfirmed");
        var service = new WorkspaceProvisioningService(new Sessions(failure != "revoked"), new Accounts(account), store, TimeProvider.System);
        Assert.Equal(WorkspaceProvisioningOutcome.Unauthenticated,
            (await service.CreateAsync(principal, "Synthetic", Guid.NewGuid(), "provision-check")).Outcome);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Owner_provisioning_requires_verified_mfa()
    {
        var user = Guid.NewGuid();
        var store = new Store();
        Assert.Equal(WorkspaceProvisioningOutcome.MfaRequired,
            (await Service(user, store).CreateAsync(Session(user, false), "Synthetic", Guid.NewGuid(), "provision-check")).Outcome);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Synthetic\nunsafe")]
    [InlineData("bad\u0000value")]
    public async Task Invalid_name_is_rejected_before_write(string name)
    {
        var user = Guid.NewGuid();
        var store = new Store();
        Assert.Equal(WorkspaceProvisioningOutcome.Rejected,
            (await Service(user, store).CreateAsync(Session(user), name, Guid.NewGuid(), "provision-check")).Outcome);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Empty_request_key_and_long_name_are_rejected_and_dependency_failure_propagates()
    {
        var user = Guid.NewGuid();
        var store = new Store();
        var service = Service(user, store);
        Assert.Equal(WorkspaceProvisioningOutcome.Rejected, (await service.CreateAsync(Session(user), "Synthetic", Guid.Empty, "provision-check")).Outcome);
        Assert.Equal(WorkspaceProvisioningOutcome.Rejected, (await service.CreateAsync(Session(user), new string('x', 257), Guid.NewGuid(), "provision-check")).Outcome);
        Assert.Equal(0, store.Calls);
        store.Fail = true;
        await Assert.ThrowsAsync<TimeoutException>(() => service.CreateAsync(Session(user), "Synthetic", Guid.NewGuid(), "provision-check"));
    }

    private static WorkspaceProvisioningService Service(Guid user, Store store) =>
        new(new Sessions(true), new Accounts(new(user, true, true)), store, TimeProvider.System);
    private sealed class Accounts(UserAccountInfo? account) : IUserAccountReader
    {
        public Task<UserAccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(account);
    }
    private sealed class Sessions(bool valid) : IAccountSessionValidator
    {
        public Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(valid);
        public Task<bool> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Store : IWorkspaceProvisioningStore
    {
        public int Calls { get; private set; }
        public Guid Owner { get; private set; }
        public Guid Request { get; private set; }
        public string? Name { get; private set; }
        public string? Correlation { get; private set; }
        public bool Fail { get; set; }
        public Task<WorkspaceProvisioningResult> CreateAsync(Guid verifiedOwnerId, string name, Guid requestId,
            string correlationId, CancellationToken cancellationToken = default)
        {
            Calls++; Owner = verifiedOwnerId; Request = requestId; Name = name; Correlation = correlationId;
            return Fail ? throw new TimeoutException("Synthetic dependency failure")
                : Task.FromResult(new WorkspaceProvisioningResult(WorkspaceProvisioningOutcome.Created, Guid.NewGuid()));
        }
    }
}
