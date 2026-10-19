using System.Net.Mail;

namespace Ramai.Modules.Identity.Domain;

public sealed class UserAccount
{
    private UserAccount(Guid userId, string email)
    {
        UserId = userId;
        Email = email;
        NormalizedEmail = email.Normalize().ToUpperInvariant();
    }

    public Guid UserId { get; }
    public string Email { get; }
    public string NormalizedEmail { get; }
    public bool IsActive { get; private set; } = true;
    public bool IsEmailConfirmed { get; private set; }
    public bool CanAccessTenantWorkspaces => IsActive && IsEmailConfirmed;

    public static UserAccount Create(Guid userId, string email)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var trimmedEmail = email.Trim();
        if (!MailAddress.TryCreate(trimmedEmail, out var address) ||
            !string.Equals(address.Address, trimmedEmail, StringComparison.Ordinal))
        {
            throw new ArgumentException("Email must be an address without a display name.", nameof(email));
        }

        return new UserAccount(userId, trimmedEmail);
    }

    // The application may call this only after validating the confirmation token.
    public void ConfirmEmail() => IsEmailConfirmed = true;

    public void Deactivate() => IsActive = false;
}
