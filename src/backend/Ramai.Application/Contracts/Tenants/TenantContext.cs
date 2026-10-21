using System.Security.Claims;

namespace Ramai.Application.Contracts.Tenants;

// Constructed only by the resolver after session and authoritative membership checks.
// A request-time snapshot, never a reusable permission cache or HTTP input DTO.
public sealed class TenantContext
{
    internal TenantContext(Guid tenantId, Guid userId, TenantAccessRole role, bool mfaVerified)
    { TenantId = tenantId; UserId = userId; Role = role; MfaVerified = mfaVerified; }
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public TenantAccessRole Role { get; }
    public bool MfaVerified { get; }
}

public enum TenantContextOutcome { Accepted, Unauthenticated, SelectionRequired, Denied, MfaRequired }

// Not a record: no automatic diagnostic formatting of the protected principal.
public sealed class TenantContextDecision
{
    internal TenantContextDecision(TenantContextOutcome outcome, TenantContext? context = null,
        ClaimsPrincipal? selectedPrincipal = null)
    { Outcome = outcome; Context = context; SelectedPrincipal = selectedPrincipal; }
    public TenantContextOutcome Outcome { get; }
    public TenantContext? Context { get; }
    // Internal host handoff only. Must be persisted in a protected ticket, never serialized as an API body.
    public ClaimsPrincipal? SelectedPrincipal { get; }
}

public interface ITenantContextResolver
{
    // Principal must originate from the configured cookie handler, never request headers/body.
    Task<TenantContextDecision> ResolveAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default);
    // An untrusted proposal, not a grant. The HTTP host must add CSRF and persist the returned principal.
    Task<TenantContextDecision> SelectAsync(ClaimsPrincipal authenticatedSession, Guid proposedTenantId,
        CancellationToken cancellationToken = default);
}

public static class TenantSessionClaims
{
    public const string SelectedTenant = "ramai.selected_tenant";
}
