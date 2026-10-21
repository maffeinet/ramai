using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ramai.Application.Contracts.Tenants;
using Ramai.Modules.Tenants.Domain;

namespace Ramai.Modules.Tenants.Persistence;

public sealed class WorkspaceProvisioningStore(IDbContextFactory<TenantsStoreContext> contexts,
    ILogger<WorkspaceProvisioningStore> logger) : IWorkspaceProvisioningStore
{
    public async Task<WorkspaceProvisioningResult> CreateAsync(Guid verifiedOwnerId, string name, Guid requestId,
        string correlationId, CancellationToken cancellationToken = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        if (verifiedOwnerId == Guid.Empty || requestId == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 256)
        { return new(WorkspaceProvisioningOutcome.Rejected); }
        await using var database = await contexts.CreateDbContextAsync(cancellationToken);
        var previous = await Replay(database, verifiedOwnerId, name, requestId, cancellationToken);
        if (previous is not null) { return Finish(previous); }
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var tenantId = Guid.NewGuid();
            database.Tenants.Add(Tenant.Create(tenantId, name, verifiedOwnerId));
            database.Set<WorkspaceProvisioningRequest>().Add(new(verifiedOwnerId, requestId, tenantId, name));
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Finish(new(WorkspaceProvisioningOutcome.Created, tenantId));
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_WorkspaceProvisioningRequests" })
        {
            // The losing transaction (including its tentative Tenant/Owner) was rolled back.
            // Never clear a caller's context or retry a failed/aborted transaction.
            await using var retry = await contexts.CreateDbContextAsync(cancellationToken);
            var replay = await Replay(retry, verifiedOwnerId, name, requestId, cancellationToken);
            if (replay is null) { throw; }
            return Finish(replay);
        }
        WorkspaceProvisioningResult Finish(WorkspaceProvisioningResult result)
        {
            logger.LogInformation("Workspace provisioning outcome {Outcome} TenantId {TenantId} UserId {UserId}",
                result.Outcome, result.TenantId, verifiedOwnerId);
            return result;
        }
    }

    private static async Task<WorkspaceProvisioningResult?> Replay(TenantsStoreContext database, Guid ownerId,
        string name, Guid requestId, CancellationToken ct)
    {
        var receipt = await database.Set<WorkspaceProvisioningRequest>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorUserId == ownerId && x.RequestId == requestId, ct);
        if (receipt is null) { return null; }
        if (receipt.RequestName != name) { return new(WorkspaceProvisioningOutcome.Conflict); }
        var ownerExists = await database.Memberships.AsNoTracking().AnyAsync(x => x.TenantId == receipt.TenantId
            && x.UserId == ownerId && x.Role == MembershipRole.Owner && x.Status == MembershipStatus.Active, ct);
        return ownerExists ? new(WorkspaceProvisioningOutcome.Replayed, receipt.TenantId)
            : new(WorkspaceProvisioningOutcome.Rejected);
    }
}
