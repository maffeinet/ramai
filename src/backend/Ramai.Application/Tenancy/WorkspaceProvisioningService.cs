using System.Security.Claims;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Application.Tenancy;

public sealed class WorkspaceProvisioningService(IAccountSessionValidator sessions, IUserAccountReader accounts,
    IWorkspaceProvisioningStore store, TimeProvider clock) : IWorkspaceProvisioner
{
    public async Task<WorkspaceProvisioningResult> CreateAsync(ClaimsPrincipal authenticatedSession, string name,
        Guid requestId, string correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SessionSubject.TryRead(authenticatedSession, clock, out var ownerId)
            || !await sessions.ValidateAsync(authenticatedSession, cancellationToken))
        { return new(WorkspaceProvisioningOutcome.Unauthenticated); }
        var account = await accounts.FindAsync(ownerId, cancellationToken);
        if (account is null || account.UserId != ownerId || !account.CanAccessTenantWorkspaces)
        { return new(WorkspaceProvisioningOutcome.Unauthenticated); }
        if (authenticatedSession.FindFirst(AccountSessionClaims.Mfa)?.Value != "true")
        { return new(WorkspaceProvisioningOutcome.MfaRequired); }
        var trimmed = name?.Trim();
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 256
            || trimmed.Any(char.IsControl) || trimmed.Contains('\uFFFD'))
        { return new(WorkspaceProvisioningOutcome.Rejected); }
        // No selected tenant is needed to create the first workspace. No client user/role/tenant input.
        return await store.CreateAsync(ownerId, trimmed, requestId, correlationId, cancellationToken);
    }
}
