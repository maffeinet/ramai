namespace Ramai.Modules.Tenants.Persistence;

// Tenant-scoped receipt: records only successful provisioning, in the same transaction.
internal sealed class WorkspaceProvisioningRequest(Guid actorUserId, Guid requestId, Guid tenantId, string requestName)
{
    public Guid ActorUserId { get; private set; } = actorUserId;
    public Guid RequestId { get; private set; } = requestId;
    public Guid TenantId { get; private set; } = tenantId;
    public string RequestName { get; private set; } = requestName;
}
