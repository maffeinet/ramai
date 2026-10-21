using System.Security.Claims;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Application.Tenancy;

public sealed class WorkspaceOperationsService(ITenantContextResolver resolver, IWorkspaceDataStore store) : IWorkspaceOperations
{
    public Task<WorkspaceOperationResult> ReadAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default) =>
        Execute(authenticatedSession, false, null, cancellationToken);

    public Task<WorkspaceOperationResult> ReadSettingsAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default) =>
        Execute(authenticatedSession, true, null, cancellationToken);

    public Task<WorkspaceOperationResult> RenameAsync(ClaimsPrincipal authenticatedSession, string name, CancellationToken cancellationToken = default) =>
        Execute(authenticatedSession, true, name ?? "", cancellationToken);

    private async Task<WorkspaceOperationResult> Execute(ClaimsPrincipal principal, bool settings, string? proposedName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Never cache a previous admission or use client role claims as an authorization source.
        var decision = await resolver.ResolveAsync(principal, ct);
        var context = decision.Context;
        if (decision.Outcome != TenantContextOutcome.Accepted || context is null)
        {
            return new(decision.Outcome switch
            {
                TenantContextOutcome.Unauthenticated => WorkspaceOperationOutcome.Unauthenticated,
                TenantContextOutcome.SelectionRequired => WorkspaceOperationOutcome.SelectionRequired,
                TenantContextOutcome.MfaRequired => WorkspaceOperationOutcome.MfaRequired,
                _ => WorkspaceOperationOutcome.Forbidden
            });
        }
        if (!Enum.IsDefined(context.Role)) { return new(WorkspaceOperationOutcome.Forbidden); }
        if (context.Role is TenantAccessRole.Owner or TenantAccessRole.Admin && !context.MfaVerified)
        { return new(WorkspaceOperationOutcome.MfaRequired); }
        if (settings && context.Role is not (TenantAccessRole.Owner or TenantAccessRole.Admin))
        { return new(WorkspaceOperationOutcome.Forbidden); }
        var name = proposedName?.Trim();
        if (proposedName is not null && (string.IsNullOrWhiteSpace(name) || name.Length > 256
            || name.Any(char.IsControl) || name.Contains('\uFFFD')))
        { return new(WorkspaceOperationOutcome.InvalidInput); }
        var workspace = proposedName is null ? await store.ReadAsync(context, ct) : await store.RenameAsync(context, name!, ct);
        // Reject a broken adapter returning any other scope or stale/untrusted role.
        if (workspace is not null && (workspace.TenantId != context.TenantId || workspace.Role != context.Role))
        { throw new InvalidOperationException("Workspace store returned an invalid scope."); }
        return workspace is null ? new(WorkspaceOperationOutcome.NotFound) : new(WorkspaceOperationOutcome.Accepted, workspace);
    }
}
