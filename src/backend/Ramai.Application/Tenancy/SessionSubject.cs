using System.Globalization;
using System.Security.Claims;
using Ramai.Application.Contracts.Identity;

namespace Ramai.Application.Tenancy;

// Shared shape/time checks for already authenticated cookie principals, not signature verification.
internal static class SessionSubject
{
    internal static bool TryRead(ClaimsPrincipal principal, TimeProvider clock, out Guid userId)
    {
        userId = Guid.Empty;
        var identities = principal.Identities.ToArray();
        return identities.Length == 1 && identities[0].IsAuthenticated
            && identities[0].AuthenticationType == AccountSessionClaims.Scheme
            && Guid.TryParse(One(principal, ClaimTypes.NameIdentifier), out userId) && userId != Guid.Empty
            && !string.IsNullOrWhiteSpace(One(principal, AccountSessionClaims.SecurityStamp))
            && long.TryParse(One(principal, AccountSessionClaims.Started), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            && start >= 0 && start <= clock.GetUtcNow().ToUnixTimeSeconds()
            && clock.GetUtcNow().ToUnixTimeSeconds() - start < AccountSessionClaims.MaximumAgeSeconds
            && principal.FindAll(AccountSessionClaims.Mfa).Count() <= 1
            && principal.FindAll(AccountSessionClaims.RecoveryGrant).Count() <= 1;
    }

    private static string? One(ClaimsPrincipal principal, string type)
    {
        var claims = principal.FindAll(type).ToArray();
        return claims.Length == 1 ? claims[0].Value : null;
    }
}
