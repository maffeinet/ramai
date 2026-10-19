namespace Ramai.Application.Contracts.Identity;

public interface IUserAccountReader
{
    // Internal application contract, not a public endpoint or authorization grant.
    Task<UserAccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken = default);
}
