using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

// Normalization adapter only: hashing/salts/verification/rehash stay in Identity.
public sealed class NormalizedPasswordHasher(IOptions<PasswordHasherOptions> options) : IPasswordHasher<AccountUser>
{
    private readonly PasswordHasher<AccountUser> _standard = new(options);

    public string HashPassword(AccountUser user, string password) =>
        _standard.HashPassword(user, PasswordText.Normalize(password)
            ?? throw new ArgumentException("Password does not satisfy length/Unicode policy.", nameof(password)));

    public PasswordVerificationResult VerifyHashedPassword(AccountUser user, string hashedPassword, string providedPassword)
    {
        var normalized = PasswordText.Normalize(providedPassword);
        try
        {
            return normalized is null ? PasswordVerificationResult.Failed : _standard.VerifyHashedPassword(user, hashedPassword, normalized);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }
}
