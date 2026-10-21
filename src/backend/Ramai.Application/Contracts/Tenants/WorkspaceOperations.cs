using System.Security.Claims;

namespace Ramai.Application.Contracts.Tenants;

public enum WorkspaceOperationOutcome
{
    Accepted, Unauthenticated, SelectionRequired, Forbidden, MfaRequired, NotFound, InvalidInput
}

// Explicit response fields only; no automatic diagnostic formatting of workspace content.
public sealed class WorkspaceInfo(Guid tenantId, string name, TenantAccessRole role)
{
    public Guid TenantId { get; } = tenantId;
    public string Name { get; } = name;
    public TenantAccessRole Role { get; } = role;
}

public sealed class WorkspaceOperationResult(WorkspaceOperationOutcome outcome, WorkspaceInfo? workspace = null)
{
    public WorkspaceOperationOutcome Outcome { get; } = outcome;
    public WorkspaceInfo? Workspace { get; } = workspace;
}

public interface IWorkspaceOperations
{
    Task<WorkspaceOperationResult> ReadAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default);
    Task<WorkspaceOperationResult> ReadSettingsAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default);
    Task<WorkspaceOperationResult> RenameAsync(ClaimsPrincipal authenticatedSession, string name, CancellationToken cancellationToken = default);
}

// Internal module port: context issued by the resolver, never an HTTP input or arbitrary tenant ID.
public interface IWorkspaceDataStore
{
    Task<WorkspaceInfo?> ReadAsync(TenantContext verifiedContext, CancellationToken cancellationToken = default);
    Task<WorkspaceInfo?> RenameAsync(TenantContext verifiedContext, string name, CancellationToken cancellationToken = default);
}
