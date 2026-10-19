using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

// One same-cost dummy verification for absent/locked/disabled users, not a timing guarantee.
public sealed class DummyPasswordWork
{
    private readonly IPasswordHasher<AccountUser> _hasher;
    private readonly AccountUser _user = new() { Id = Guid.NewGuid() };
    private readonly string _hash;

    public DummyPasswordWork(IPasswordHasher<AccountUser> hasher)
    {
        _hasher = hasher;
        _hash = hasher.HashPassword(_user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
    public void Perform(string? password) => _hasher.VerifyHashedPassword(_user, _hash,
        PasswordText.Normalize(password) ?? "synthetic-invalid-input-value");
}
