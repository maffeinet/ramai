using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Ramai.Modules.Identity.Persistence.Sessions;

// Identity's default EF store stores authenticator/recovery material as plaintext.
// Protect MFA material, purpose-bound to the owner and token slot; no plaintext fallback.
// Other token slots (e.g. B's one-use digests) retain their existing format.
public sealed class ProtectedIdentityTokenStore(IdentityStoreContext context, IDataProtectionProvider protection,
    IdentityErrorDescriber describer) : UserOnlyStore<AccountUser, IdentityStoreContext, Guid>(context, describer)
{
    private IDataProtector Protector(AccountUser user, string provider, string name) =>
        protection.CreateProtector("RAMAI.IdentityTokens.v1", user.Id.ToString("D"), provider, name);
    private static bool Sensitive(string provider, string name) => provider == "RAMAI.Mfa.v1"
        || (provider == "[AspNetUserStore]" && name is "AuthenticatorKey" or "RecoveryCodes");

    public override async Task<string?> GetTokenAsync(AccountUser user, string loginProvider, string name,
        CancellationToken cancellationToken = default)
    {
        var value = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);
        return value is null || !Sensitive(loginProvider, name) ? value : Protector(user, loginProvider, name).Unprotect(value);
    }

    public override Task SetTokenAsync(AccountUser user, string loginProvider, string name, string? value,
        CancellationToken cancellationToken = default) => base.SetTokenAsync(user, loginProvider, name,
            value is null || !Sensitive(loginProvider, name) ? value : Protector(user, loginProvider, name).Protect(value), cancellationToken);
}
