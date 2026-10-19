using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public sealed class AccountPasswordValidator : IPasswordValidator<AccountUser>
{
    private static readonly FrozenSet<string> Denied = Load();
    public static int CorpusEntryCount => Denied.Count;

    public Task<IdentityResult> ValidateAsync(UserManager<AccountUser> manager, AccountUser user, string? password)
    {
        var normalized = PasswordText.Normalize(password);
        var valid = normalized is not null && !IsDenied(normalized, user.Email);
        return Task.FromResult(valid ? IdentityResult.Success : IdentityResult.Failed(new IdentityError
        {
            Code = "PasswordPolicy",
            Description = "Choose a password of 15–128 Unicode characters that is not common or account-related."
        }));
    }

    private static bool IsDenied(string password, string? email)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password.ToUpperInvariant())));
        if (Denied.Contains(digest) || password.Contains("ramai", StringComparison.OrdinalIgnoreCase) ||
            password.EnumerateRunes().Distinct().Count() == 1)
        {
            return true;
        }
        var local = email?.Split('@')[0];
        return local is { Length: >= 4 } && password.Contains(local, StringComparison.OrdinalIgnoreCase);
    }

    private static FrozenSet<string> Load()
    {
        using var stream = typeof(AccountPasswordValidator).Assembly.GetManifestResourceStream(
            "Ramai.Modules.Identity.Persistence.AccountSecurity.Data.ncsc-eligible.sha256")
            ?? throw new InvalidOperationException("Required password denylist is unavailable.");
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0 || lines.Any(line => line.Length != 64 || !line.All(char.IsAsciiHexDigit)))
        {
            throw new InvalidOperationException("Required password denylist is invalid.");
        }
        return lines.ToFrozenSet(StringComparer.Ordinal);
    }
}
