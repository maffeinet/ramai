using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Tenants;
using Ramai.Modules.Tenants.Domain;

namespace Ramai.Modules.Tenants.Persistence;

public sealed class TenantMembershipReader(TenantsStoreContext context) : ITenantMembershipReader
{
    public async Task<TenantMembershipInfo?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await context.Memberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Status == MembershipStatus.Active, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var role = membership.Role switch
        {
            MembershipRole.Owner => TenantAccessRole.Owner,
            MembershipRole.Admin => TenantAccessRole.Admin,
            MembershipRole.Member => TenantAccessRole.Member,
            _ => throw new InvalidOperationException("Unknown stored membership role.")
        };
        return new TenantMembershipInfo(membership.TenantId, membership.UserId, role);
    }
}
