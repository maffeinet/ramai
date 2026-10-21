using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Modules.Tenants.Persistence;

// Receives a freshly verified application context; never queries global/bootstrap data.
public sealed class WorkspaceDataStore(DbContextOptions<TenantDataContext> options) : IWorkspaceDataStore
{
    public async Task<WorkspaceInfo?> ReadAsync(TenantContext verifiedContext, CancellationToken cancellationToken = default)
    {
        await using var db = new TenantDataContext(options, verifiedContext);
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? null : new(tenant.TenantId, tenant.Name, verifiedContext.Role);
    }

    public async Task<WorkspaceInfo?> RenameAsync(TenantContext verifiedContext, string name, CancellationToken cancellationToken = default)
    {
        if (verifiedContext is null || !verifiedContext.MfaVerified
            || verifiedContext.Role is not (TenantAccessRole.Owner or TenantAccessRole.Admin))
        { throw new UnauthorizedAccessException("Workspace settings permission is required."); }
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name != name.Trim()
            || name.Any(char.IsControl) || name.Contains('\uFFFD'))
        { throw new ArgumentException("Invalid workspace name.", nameof(name)); }
        await using var db = new TenantDataContext(options, verifiedContext);
        var tenant = await db.Tenants.SingleOrDefaultAsync(cancellationToken);
        if (tenant is null) { return null; }
        // The only editable setting in this slice. Never bind a domain entity from HTTP input.
        db.Entry(tenant).Property(x => x.Name).CurrentValue = name;
        await db.SaveChangesAsync(cancellationToken);
        return new(tenant.TenantId, name, verifiedContext.Role);
    }
}
