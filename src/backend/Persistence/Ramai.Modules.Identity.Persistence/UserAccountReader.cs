using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Identity;

namespace Ramai.Modules.Identity.Persistence;

public sealed class UserAccountReader(IdentityStoreContext context) : IUserAccountReader
{
    public Task<UserAccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new UserAccountInfo(user.Id, user.IsActive, user.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
}
