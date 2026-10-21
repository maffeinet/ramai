using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ramai.Application.Contracts.Identity;
using Ramai.Modules.Identity.Persistence.AccountSecurity;

namespace Ramai.Modules.Identity.Persistence.Sessions;

public sealed class AccountMfaService(IdentityStoreContext database, UserManager<AccountUser> manager,
    AccountRateLimiter limiter, TimeProvider clock, ILogger<AccountMfaService> logger) : IAccountMfaService
{
    private const string Provider = "RAMAI.Mfa.v1";
    private const string Slot = "State";

    public Task<MfaDecision> ManageAsync(ClaimsPrincipal principal, MfaOperation operation, string password,
        string? code, string? recoveryCode, IPAddress origin, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        { return Task.FromResult(new MfaDecision(LoginOutcome.Rejected)); }
        return Run(id, principal.FindFirstValue(AccountSessionClaims.SecurityStamp) ?? "", operation,
            password, code, recoveryCode, origin, cancellationToken,
            principal.FindFirstValue(AccountSessionClaims.RecoveryGrant));
    }

    public Task<MfaDecision> VerifyLoginAsync(Guid userId, string stamp, string? code, string? recoveryCode,
        IPAddress origin, CancellationToken cancellationToken = default) =>
        Run(userId, stamp, null, null, code, recoveryCode, origin, cancellationToken);

    private async Task<MfaDecision> Run(Guid id, string stamp, MfaOperation? operation, string? password,
        string? code, string? recoveryCode, IPAddress origin, CancellationToken ct, string? recoveryGrant = null)
    {
        if (!await limiter.AllowAsync(false, "mfa:" + id.ToString("D"), origin, ct))
        { return new(LoginOutcome.Throttled); }
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM identity.\"Users\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null) { return new(LoginOutcome.Rejected); }
        await database.Entry(user).ReloadAsync(ct);
        if (!user.IsActive || !user.EmailConfirmed || !user.LockoutEnabled || user.SecurityStamp != stamp
            || string.IsNullOrEmpty(stamp) || await manager.IsLockedOutAsync(user)) { return new(LoginOutcome.Rejected); }
        // Token rows must be refreshed too when a scoped context is reused after another instance's commit.
        foreach (var entry in database.ChangeTracker.Entries<IdentityUserToken<Guid>>().ToArray())
        { entry.State = EntityState.Detached; }
        var serialized = await manager.GetAuthenticationTokenAsync(user, Provider, Slot);
        var state = serialized is null ? new State() : JsonSerializer.Deserialize<State>(serialized)
            ?? throw new CryptographicException("MFA state unavailable.");
        var now = clock.GetUtcNow();
        if (state.LockedUntil > now) { return new(LoginOutcome.Throttled); }
        if (state.LockedUntil is not null) { state.Failures = 0; state.LockedUntil = null; }
        if (operation is not null && (PasswordText.Normalize(password!) is null
            || !await manager.CheckPasswordAsync(user, password!))) { return await Failure(); }

        if (operation == MfaOperation.ConfirmEnrollment)
        {
            if (state.PendingKey is null || state.PendingUntil is null || state.PendingUntil <= now
                || state.PendingStamp != user.SecurityStamp) { return await Failure(); }
            // Replacement remains uncommitted until proof succeeds; rollback retains the previous authenticator.
            var previousKey = await manager.GetAuthenticatorKeyAsync(user);
            Ensure(await manager.SetAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey", state.PendingKey));
            if (!await ValidTotp())
            {
                if (previousKey is null) { Ensure(await manager.RemoveAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey")); }
                else { Ensure(await manager.SetAuthenticationTokenAsync(user, "[AspNetUserStore]", "AuthenticatorKey", previousKey)); }
                return await Failure();
            }
            Ensure(await manager.SetTwoFactorEnabledAsync(user, true));
            state.PendingKey = null;
            state.PendingUntil = null;
            state.PendingStamp = null;
            state.Failures = 0;
            var codes = await NewRecoveryCodes();
            Ensure(await manager.UpdateSecurityStampAsync(user));
            await Save();
            await transaction.CommitAsync(ct);
            Audit("EnrollmentConfirmed");
            return new(LoginOutcome.Accepted, recoveryCodes: codes);
        }

        string? newRecoveryGrant = null;
        if (user.TwoFactorEnabled)
        {
            var recovery = !string.IsNullOrEmpty(recoveryCode);
            var recentRecovery = operation is not null && code is null && recoveryCode is null
                && recoveryGrant is { Length: 64 } && state.GrantUntil > now && state.GrantStamp == user.SecurityStamp
                && state.GrantDigest is not null && CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(Encoding.UTF8.GetBytes(recoveryGrant)), Convert.FromHexString(state.GrantDigest));
            var valid = recentRecovery || (recovery && string.IsNullOrEmpty(code)
                ? (await manager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode!)).Succeeded
                : !recovery && await ValidTotp());
            if (!valid) { return await Failure(); }
            if (recentRecovery) { state.GrantDigest = null; state.GrantUntil = null; state.GrantStamp = null; }
            if (recovery)
            {
                var previousStamp = user.SecurityStamp;
                Ensure(await manager.UpdateSecurityStampAsync(user));
                // A fresh password+recovery login may continue this already-authorized pending rotation.
                // Never rebind a pending invalidated by an intervening reset/revocation/stamp change.
                if (state.PendingKey is not null && state.PendingUntil > now && state.PendingStamp == previousStamp)
                { state.PendingStamp = user.SecurityStamp; }
                if (operation is null)
                {
                    // A single remaining recovery code must suffice to restore a lost authenticator.
                    // Not an HTTP token: protected session claim, bound to this stamp, one use, five minutes.
                    newRecoveryGrant = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                    state.GrantDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newRecoveryGrant)));
                    state.GrantUntil = now.AddMinutes(5);
                    state.GrantStamp = user.SecurityStamp;
                }
            }
        }
        else if (operation is null || operation == MfaOperation.RegenerateRecoveryCodes)
        { return new(LoginOutcome.Rejected); }

        state.Failures = 0;
        MfaDecision result;
        if (operation == MfaOperation.BeginEnrollment)
        {
            state.PendingKey = manager.GenerateNewAuthenticatorKey();
            state.PendingUntil = now.AddMinutes(10);
            state.PendingStamp = user.SecurityStamp;
            result = new(LoginOutcome.Accepted, secret: state.PendingKey,
                stamp: user.TwoFactorEnabled && !string.IsNullOrEmpty(recoveryCode) ? user.SecurityStamp : null);
        }
        else if (operation == MfaOperation.RegenerateRecoveryCodes)
        {
            var codes = await NewRecoveryCodes();
            Ensure(await manager.UpdateSecurityStampAsync(user));
            result = new(LoginOutcome.Accepted, recoveryCodes: codes);
        }
        else { result = new(LoginOutcome.Accepted, stamp: user.SecurityStamp, recoveryGrant: newRecoveryGrant); }
        await Save();
        await transaction.CommitAsync(ct);
        Audit(operation?.ToString() ?? "SecondFactorVerified");
        return result;

        async Task<bool> ValidTotp()
        {
            if (code is null || code.Length != 6 || !code.All(char.IsAsciiDigit)) { return false; }
            state.UsedCodes.RemoveAll(x => x.Until <= now);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
            if (state.UsedCodes.Any(x => x.Digest == digest)) { return false; }
            if (!await manager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code)) { return false; }
            // Identity accepts a bounded clock-skew window. Retain the digest beyond its full acceptance window.
            state.UsedCodes.Add(new UsedCode { Digest = digest, Until = now.AddMinutes(3) });
            return true;
        }
        async Task Save() => Ensure(await manager.SetAuthenticationTokenAsync(user, Provider, Slot, JsonSerializer.Serialize(state)));
        async Task<string[]> NewRecoveryCodes()
        {
            var codes = (await manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
            return codes is { Length: 10 } ? codes : throw new DbUpdateException("MFA state update failed.");
        }
        async Task<MfaDecision> Failure()
        {
            state.Failures++;
            if (state.Failures >= 5) { state.LockedUntil = now.AddMinutes(15); }
            await Save();
            await transaction.CommitAsync(ct);
            Audit("Rejected");
            return new(state.LockedUntil is null ? LoginOutcome.Rejected : LoginOutcome.Throttled);
        }
        void Audit(string outcome) => logger.LogInformation("Account MFA operation {Operation} outcome {Outcome} UserId {UserId}",
            operation?.ToString() ?? "Login", outcome, id);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) { throw new DbUpdateException("MFA state update failed."); }
    }

    private sealed class State
    {
        public string? PendingKey { get; set; }
        public DateTimeOffset? PendingUntil { get; set; }
        public string? PendingStamp { get; set; }
        public string? GrantDigest { get; set; }
        public string? GrantStamp { get; set; }
        public DateTimeOffset? GrantUntil { get; set; }
        public int Failures { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
        public List<UsedCode> UsedCodes { get; set; } = [];
    }
    private sealed class UsedCode
    {
        public string Digest { get; set; } = "";
        public DateTimeOffset Until { get; set; }
    }
}
