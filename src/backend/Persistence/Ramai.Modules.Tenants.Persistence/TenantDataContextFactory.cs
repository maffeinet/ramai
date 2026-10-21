using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Modules.Tenants.Persistence;

public sealed class TenantDataContextFactory(DbContextOptions<TenantDataContext> options, ITenantContextResolver resolver)
{
    public async Task<TenantDataContext> CreateAsync(ClaimsPrincipal authenticatedSession, CancellationToken cancellationToken = default)
    {
        var decision = await resolver.ResolveAsync(authenticatedSession, cancellationToken);
        if (decision.Outcome != TenantContextOutcome.Accepted || decision.Context is null)
        { throw new UnauthorizedAccessException("A verified tenant context is required."); }
        return new TenantDataContext(options, decision.Context);
    }
}
