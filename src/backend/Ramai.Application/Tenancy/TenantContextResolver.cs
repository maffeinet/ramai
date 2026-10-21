using System.Security.Claims;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Application.Tenancy;

// No HttpContext, mutable current-tenant singleton, AsyncLocal, EF or module tables.
// Register scoped at the eventual HTTP composition point; every call revalidates dependencies.
public sealed class TenantContextResolver(IAccountSessionValidator sessions, ITenantMembershipReader memberships,
    TimeProvider clock) : ITenantContextResolver
{
    public Task<TenantContextDecision> ResolveAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default) =>
        Resolve(authenticatedSession, null, false, cancellationToken);

    public Task<TenantContextDecision> SelectAsync(ClaimsPrincipal authenticatedSession, Guid proposedTenantId,
        CancellationToken cancellationToken = default) => Resolve(authenticatedSession, proposedTenantId, true, cancellationToken);

    private async Task<TenantContextDecision> Resolve(ClaimsPrincipal principal, Guid? proposedTenant, bool selection,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!SessionSubject.TryRead(principal, clock, out var userId) || !await sessions.ValidateAsync(principal, ct))
        { return new(TenantContextOutcome.Unauthenticated); }

        var tenantClaims = principal.FindAll(TenantSessionClaims.SelectedTenant).ToArray();
        if (tenantClaims.Length > 1) { return new(TenantContextOutcome.Denied); }
        if (tenantClaims.Length == 1 && (!Guid.TryParse(tenantClaims[0].Value, out var existingTenant)
            || existingTenant == Guid.Empty)) { return new(TenantContextOutcome.Denied); }
        Guid tenantId;
        if (selection)
        {
            tenantId = proposedTenant!.Value;
            if (tenantId == Guid.Empty) { return new(TenantContextOutcome.Denied); }
        }
        else
        {
            if (tenantClaims.Length == 0) { return new(TenantContextOutcome.SelectionRequired); }
            if (!Guid.TryParse(tenantClaims[0].Value, out tenantId) || tenantId == Guid.Empty)
            { return new(TenantContextOutcome.Denied); }
        }
        var membership = await memberships.FindActiveAsync(tenantId, userId, ct);
        if (membership is null || membership.TenantId != tenantId || membership.UserId != userId
            || !Enum.IsDefined(membership.Role)) { return new(TenantContextOutcome.Denied); }
        var mfa = One(AccountSessionClaims.Mfa) == "true";
        if (membership.Role is TenantAccessRole.Owner or TenantAccessRole.Admin && !mfa)
        { return new(TenantContextOutcome.MfaRequired); }

        var context = new TenantContext(tenantId, userId, membership.Role, mfa);
        if (!selection) { return new(TenantContextOutcome.Accepted, context); }
        // Only first-party session markers are retained; roles are always read from membership, not cookies.
        string[] retained = [ClaimTypes.NameIdentifier, AccountSessionClaims.SecurityStamp, AccountSessionClaims.Started,
            AccountSessionClaims.Mfa, AccountSessionClaims.RecoveryGrant];
        var claims = principal.Claims.Where(x => retained.Contains(x.Type)).Select(x => new Claim(x.Type, x.Value)).ToList();
        claims.Add(new Claim(TenantSessionClaims.SelectedTenant, tenantId.ToString("D")));
        var selected = new ClaimsPrincipal(new ClaimsIdentity(claims, AccountSessionClaims.Scheme));
        return new(TenantContextOutcome.Accepted, context, selected);

        string? One(string type)
        {
            var values = principal.FindAll(type).ToArray();
            return values.Length == 1 ? values[0].Value : null;
        }
    }
}
