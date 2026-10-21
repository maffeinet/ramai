using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Identity;
using Ramai.Modules.Identity.Persistence.AccountSecurity;

namespace Ramai.Modules.Identity.Persistence.Sessions;

public sealed class AccountLoginService(AccountCredentialService credentials, TimeProvider clock, IAccountMfaService mfa) : IAccountLoginService
{
    public async Task<LoginDecision> CheckAsync(string email, string password, IPAddress origin, string correlationId,
        CancellationToken cancellationToken = default, string? code = null, string? recoveryCode = null)
    {
        var result = await credentials.CheckCredentialsAsync(email, password,
            new AccountRequestContext(origin, correlationId), cancellationToken);
        if (result.Status != AccountOperationStatus.Accepted)
        {
            return new(result.Status switch
            {
                AccountOperationStatus.Throttled => LoginOutcome.Throttled,
                AccountOperationStatus.Unavailable => LoginOutcome.Unavailable,
                _ => LoginOutcome.Rejected
            });
        }
        if (result.UserId is null || string.IsNullOrEmpty(result.VerifiedStamp))
        {
            return new(LoginOutcome.Rejected);
        }
        var stamp = result.VerifiedStamp;
        string? recoveryGrant = null;
        if (result.RequiresSecondFactor)
        {
            var secondFactor = await mfa.VerifyLoginAsync(result.UserId.Value, stamp, code, recoveryCode, origin, cancellationToken);
            if (secondFactor.Outcome != LoginOutcome.Accepted) { return new(secondFactor.Outcome); }
            stamp = secondFactor.Stamp!;
            recoveryGrant = secondFactor.RecoveryGrant;
        }
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, result.UserId.Value.ToString("D")),
            new Claim(AccountSessionClaims.SecurityStamp, stamp),
            new Claim(AccountSessionClaims.Started, clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        ], AccountSessionClaims.Scheme);
        if (result.RequiresSecondFactor) { identity.AddClaim(new Claim(AccountSessionClaims.Mfa, "true")); }
        if (recoveryGrant is not null) { identity.AddClaim(new Claim(AccountSessionClaims.RecoveryGrant, recoveryGrant)); }
        return new(LoginOutcome.Accepted, new ClaimsPrincipal(identity));
    }
}

public sealed class AccountSessionValidator(IdentityStoreContext database, UserManager<AccountUser> manager)
    : IAccountSessionValidator
{
    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { return false; }
        // No cache/tracked account: changes committed before this check must be observed.
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is { IsActive: true, EmailConfirmed: true, LockoutEnabled: true }
            && (!user.TwoFactorEnabled || principal.FindFirstValue(AccountSessionClaims.Mfa) == "true")
            && (user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow)
            && !string.IsNullOrEmpty(user.SecurityStamp)
            && user.SecurityStamp == principal.FindFirstValue(AccountSessionClaims.SecurityStamp);
    }

    public async Task<bool> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM identity.\"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (user is null) { return false; }
        await database.Entry(user).ReloadAsync(cancellationToken);
        if (!(await manager.UpdateSecurityStampAsync(user)).Succeeded) { return false; }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
