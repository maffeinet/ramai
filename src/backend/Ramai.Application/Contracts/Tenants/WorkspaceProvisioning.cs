using System.Security.Claims;

namespace Ramai.Application.Contracts.Tenants;

public enum WorkspaceProvisioningOutcome { Created, Replayed, Conflict, Rejected, Unauthenticated, MfaRequired }

// No generated ToString containing the workspace name or authenticated principal.
public sealed class WorkspaceProvisioningResult(WorkspaceProvisioningOutcome outcome, Guid? tenantId = null)
{
    public WorkspaceProvisioningOutcome Outcome { get; } = outcome;
    public Guid? TenantId { get; } = tenantId;
}

public interface IWorkspaceProvisioner
{
    Task<WorkspaceProvisioningResult> CreateAsync(ClaimsPrincipal authenticatedSession, string name, Guid requestId,
        string correlationId, CancellationToken cancellationToken = default);
}

// Internal application port, not an HTTP model or authorization boundary.
// The application passes the verified subject; persistence writes only the Tenants module.
public interface IWorkspaceProvisioningStore
{
    Task<WorkspaceProvisioningResult> CreateAsync(Guid verifiedOwnerId, string name, Guid requestId,
        string correlationId, CancellationToken cancellationToken = default);
}
