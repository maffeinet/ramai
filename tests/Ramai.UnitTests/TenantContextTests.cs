using System.Security.Claims;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;

namespace Ramai.UnitTests;

public sealed class TenantContextTests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _tenant = Guid.NewGuid();
    private static ClaimsPrincipal Session(Guid user, Guid? tenant = null, bool mfa = false)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, user.ToString()), new(AccountSessionClaims.SecurityStamp, "synthetic-marker"),
            new(AccountSessionClaims.Started, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)) };
        if (tenant is not null) { claims.Add(new(TenantSessionClaims.SelectedTenant, tenant.ToString()!)); }
        if (mfa) { claims.Add(new(AccountSessionClaims.Mfa, "true")); }
        return new(new ClaimsIdentity(claims, AccountSessionClaims.Scheme));
    }

    [Fact]
    public async Task No_selected_tenant_never_falls_back_to_first_membership()
    {
        var reader = new Reader(new(_tenant, _user, TenantAccessRole.Member));
        var result = await Resolver(reader).ResolveAsync(Session(_user));
        Assert.Equal(TenantContextOutcome.SelectionRequired, result.Outcome);
        Assert.Null(result.Context);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("other-scheme")]
    [InlineData("duplicate-user")]
    [InlineData("duplicate-stamp")]
    [InlineData("empty-user")]
    [InlineData("duplicate-start")]
    [InlineData("duplicate-mfa")]
    [InlineData("extra-identity")]
    [InlineData("future-start")]
    [InlineData("expired-start")]
    public async Task Invalid_or_ambiguous_session_never_queries_membership(string corruption)
    {
        var principal = Session(_user, _tenant, true);
        var identity = (ClaimsIdentity)principal.Identity!;
        switch (corruption)
        {
            case "anonymous": principal = new(new ClaimsIdentity(identity.Claims)); break;
            case "other-scheme": principal = new(new ClaimsIdentity(identity.Claims, "Other")); break;
            case "duplicate-user": identity.AddClaim(new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())); break;
            case "duplicate-stamp": identity.AddClaim(new(AccountSessionClaims.SecurityStamp, "other")); break;
            case "empty-user": identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)); identity.AddClaim(new(ClaimTypes.NameIdentifier, Guid.Empty.ToString())); break;
            case "duplicate-start": identity.AddClaim(new(AccountSessionClaims.Started, "0")); break;
            case "duplicate-mfa": identity.AddClaim(new(AccountSessionClaims.Mfa, "true")); break;
            case "extra-identity": principal.AddIdentity(new ClaimsIdentity()); break;
            default:
                identity.RemoveClaim(identity.FindFirst(AccountSessionClaims.Started));
                var start = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (corruption == "future-start" ? 300 : -AccountSessionClaims.MaximumAgeSeconds);
                identity.AddClaim(new(AccountSessionClaims.Started, start.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                break;
        }
        var reader = new Reader(new(_tenant, _user, TenantAccessRole.Member));
        Assert.Equal(TenantContextOutcome.Unauthenticated, (await Resolver(reader).ResolveAsync(principal)).Outcome);
        Assert.Equal(0, reader.Calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("invalid")]
    public async Task Invalid_tenant_claim_fails_closed(string corruption)
    {
        var principal = Session(_user, _tenant);
        var identity = (ClaimsIdentity)principal.Identity!;
        if (corruption != "duplicate") { identity.RemoveClaim(identity.FindFirst(TenantSessionClaims.SelectedTenant)); }
        identity.AddClaim(new(TenantSessionClaims.SelectedTenant, corruption == "empty" ? Guid.Empty.ToString() : "untrusted-value"));
        Assert.Equal(TenantContextOutcome.Denied, (await Resolver(new Reader(null)).ResolveAsync(principal)).Outcome);
        Assert.Equal(TenantContextOutcome.Denied, (await Resolver(new Reader(new(_tenant, _user, TenantAccessRole.Member)))
            .SelectAsync(principal, _tenant)).Outcome);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("wrong-user")]
    [InlineData("wrong-tenant")]
    [InlineData("unknown-role")]
    public async Task Missing_or_mismatched_membership_is_never_a_context(string mismatch)
    {
        TenantMembershipInfo? membership = mismatch switch
        {
            "absent" => null,
            "wrong-user" => new(_tenant, Guid.NewGuid(), TenantAccessRole.Owner),
            "wrong-tenant" => new(Guid.NewGuid(), _user, TenantAccessRole.Member),
            _ => new(_tenant, _user, (TenantAccessRole)99)
        };
        var result = await Resolver(new Reader(membership)).ResolveAsync(Session(_user, _tenant, true));
        Assert.Equal(TenantContextOutcome.Denied, result.Outcome);
        Assert.Null(result.Context);
    }

    [Theory]
    [InlineData(TenantAccessRole.Owner, false, TenantContextOutcome.MfaRequired)]
    [InlineData(TenantAccessRole.Admin, false, TenantContextOutcome.MfaRequired)]
    [InlineData(TenantAccessRole.Member, false, TenantContextOutcome.Accepted)]
    [InlineData(TenantAccessRole.Owner, true, TenantContextOutcome.Accepted)]
    [InlineData(TenantAccessRole.Admin, true, TenantContextOutcome.Accepted)]
    public async Task Privileged_context_requires_verified_mfa(TenantAccessRole role, bool mfa, TenantContextOutcome outcome)
    {
        var result = await Resolver(new Reader(new(_tenant, _user, role))).ResolveAsync(Session(_user, _tenant, mfa));
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(outcome == TenantContextOutcome.Accepted, result.Context is not null);
    }

    [Fact]
    public async Task Selection_validates_membership_and_does_not_mutate_input_or_extend_absolute_start()
    {
        var principal = Session(_user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new(ClaimTypes.Role, "Owner"));
        var result = await Resolver(new Reader(new(_tenant, _user, TenantAccessRole.Member))).SelectAsync(principal, _tenant);
        Assert.Equal(TenantContextOutcome.Accepted, result.Outcome);
        Assert.Equal(TenantAccessRole.Member, result.Context!.Role);
        Assert.Null(principal.FindFirst(TenantSessionClaims.SelectedTenant));
        var selected = Assert.IsType<ClaimsPrincipal>(result.SelectedPrincipal);
        Assert.Equal(_tenant.ToString(), selected.FindFirstValue(TenantSessionClaims.SelectedTenant));
        Assert.Null(selected.FindFirst(ClaimTypes.Role));
        Assert.Equal(principal.FindFirstValue(AccountSessionClaims.Started), selected.FindFirstValue(AccountSessionClaims.Started));
        Assert.Equal(TenantContextOutcome.Denied, (await Resolver(new Reader(null)).SelectAsync(principal, Guid.Empty)).Outcome);
    }

    [Fact]
    public async Task Revoked_session_and_dependency_failure_cannot_issue_context()
    {
        var reader = new Reader(new(_tenant, _user, TenantAccessRole.Member));
        var revoked = new TenantContextResolver(new Sessions(false), reader, TimeProvider.System);
        Assert.Equal(TenantContextOutcome.Unauthenticated, (await revoked.ResolveAsync(Session(_user, _tenant))).Outcome);
        Assert.Equal(0, reader.Calls);
        await Assert.ThrowsAsync<TimeoutException>(() => Resolver(new BrokenReader()).ResolveAsync(Session(_user, _tenant)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Resolver(reader).ResolveAsync(Session(_user), cancellation.Token));
    }

    private static TenantContextResolver Resolver(ITenantMembershipReader reader) => new(new Sessions(true), reader, TimeProvider.System);
    private sealed class Reader(TenantMembershipInfo? membership) : ITenantMembershipReader
    {
        public int Calls { get; private set; }
        public Task<TenantMembershipInfo?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(membership); }
    }
    private sealed class BrokenReader : ITenantMembershipReader
    {
        public Task<TenantMembershipInfo?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
            throw new TimeoutException("Synthetic dependency failure.");
    }
    private sealed class Sessions(bool valid) : IAccountSessionValidator
    {
        public Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(valid);
        public Task<bool> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
