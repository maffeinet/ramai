using System.Data.Common;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using StackExchange.Redis;

namespace Ramai.Modules.Identity.Persistence.AccountRecovery;

public sealed class AccountRecoveryService(UserManager<AccountUser> manager, IdentityStoreContext database,
    AccountRateLimiter limiter, IAccountRecoverySender sender, RecoveryLinks links, ILogger<AccountRecoveryService> logger)
{
    public const string MarkerProvider = "Ramai.AccountRecovery.v1";

    public Task<AccountOperationResult> RequestConfirmationAsync(string email, string returnPath, AccountRequestContext request,
        CancellationToken cancellationToken = default) => RequestAsync(true, email, returnPath, request, cancellationToken);

    public Task<AccountOperationResult> RequestResetAsync(string email, string returnPath, AccountRequestContext request,
        CancellationToken cancellationToken = default) => RequestAsync(false, email, returnPath, request, cancellationToken);

    public Task<AccountOperationResult> ConfirmEmailAsync(Guid userId, string code, AccountRequestContext request,
        CancellationToken cancellationToken = default) => ConsumeAsync(true, userId, code, null, request, cancellationToken);

    public Task<AccountOperationResult> ResetPasswordAsync(Guid userId, string code, string newPassword, AccountRequestContext request,
        CancellationToken cancellationToken = default) => ConsumeAsync(false, userId, code, newPassword, request, cancellationToken);

    private async Task<AccountOperationResult> RequestAsync(bool confirmation, string email, string returnPath,
        AccountRequestContext request, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = request.CorrelationId });
        var operation = confirmation ? AccountRateOperation.RequestEmailConfirmation : AccountRateOperation.RequestPasswordReset;
        try
        {
            var trimmed = email?.Trim();
            if (!RecoveryLinks.IsSafeReturnPath(returnPath) || trimmed is null || trimmed.Length > 256 ||
                !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
            {
                return Finish(operation, AccountOperationStatus.Rejected);
            }
            var normalized = manager.NormalizeEmail(trimmed);
            if (!await limiter.AllowAsync(operation, normalized, request.Origin, cancellationToken))
            {
                return Finish(operation, AccountOperationStatus.Throttled);
            }
            AccountRecoveryMessage? message = null;
            await using (var transaction = await database.Database.BeginTransactionAsync(cancellationToken))
            {
                var user = await database.Users.FromSqlInterpolated(
                    $"SELECT * FROM identity.\"Users\" WHERE \"NormalizedEmail\" = {normalized} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (user is not null) { await database.Entry(user).ReloadAsync(cancellationToken); }
                if (Eligible(user, confirmation))
                {
                    var token = confirmation ? await manager.GenerateEmailConfirmationTokenAsync(user!) : await manager.GeneratePasswordResetTokenAsync(user!);
                    var name = MarkerName(confirmation);
                    var digest = Digest(token);
                    await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO identity.\"UserTokens\" (\"UserId\", \"LoginProvider\", \"Name\", \"Value\") VALUES ({user!.Id}, {MarkerProvider}, {name}, {digest}) ON CONFLICT (\"UserId\", \"LoginProvider\", \"Name\") DO UPDATE SET \"Value\" = EXCLUDED.\"Value\"", cancellationToken);
                    message = new AccountRecoveryMessage(user.Email!, confirmation ? RecoveryPurpose.EmailConfirmation : RecoveryPurpose.PasswordReset,
                        links.Create(confirmation, user.Id, token, returnPath));
                }
                await transaction.CommitAsync(cancellationToken);
            }
            if (message is not null)
            {
                try { await sender.SendAsync(message, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // A delivery error must not reveal account existence or a bearer URL.
                    // Persisted latest marker remains; caller can request a new link later.
                    logger.LogWarning("Account recovery delivery failed for operation {Operation}", operation);
                }
            }
            return Finish(operation, AccountOperationStatus.Accepted);
        }
        catch (Exception exception) when (IsDependencyFailure(exception)) { return Finish(operation, AccountOperationStatus.Unavailable); }
    }

    private async Task<AccountOperationResult> ConsumeAsync(bool confirmation, Guid userId, string code, string? password,
        AccountRequestContext request, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = request.CorrelationId });
        var operation = confirmation ? AccountRateOperation.ConfirmEmail : AccountRateOperation.ResetPassword;
        try
        {
            if (!await limiter.AllowAsync(operation, userId.ToString("N"), request.Origin, cancellationToken))
            {
                return Finish(operation, AccountOperationStatus.Throttled);
            }
            var token = RecoveryLinks.DecodeCode(code);
            if (userId == Guid.Empty || token is null) { return Finish(operation, AccountOperationStatus.Rejected); }
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var user = await database.Users.FromSqlInterpolated(
                $"SELECT * FROM identity.\"Users\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (user is not null) { await database.Entry(user).ReloadAsync(cancellationToken); }
            if (!Eligible(user, confirmation)) { return Finish(operation, AccountOperationStatus.Rejected); }
            var name = MarkerName(confirmation);
            var marker = await database.UserTokens.AsNoTracking().SingleOrDefaultAsync(x =>
                x.UserId == userId && x.LoginProvider == MarkerProvider && x.Name == name, cancellationToken);
            if (!Matches(marker?.Value, token)) { return Finish(operation, AccountOperationStatus.Rejected); }
            if (!confirmation && PasswordText.Normalize(password) is null) { return Finish(operation, AccountOperationStatus.Rejected); }
            var result = confirmation ? await manager.ConfirmEmailAsync(user!, token) :
                await manager.ResetPasswordAsync(user!, token, password!);
            if (!result.Succeeded) { return Finish(operation, AccountOperationStatus.Rejected); }
            await database.UserTokens.Where(x => x.UserId == userId && x.LoginProvider == MarkerProvider && x.Name == name)
                .ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            // SecurityStamp/password update and marker consumption commit atomically.
            // No session is issued or claimed revoked here: session integration is S1-004C.
            return Finish(operation, AccountOperationStatus.Accepted);
        }
        catch (Exception exception) when (IsDependencyFailure(exception)) { return Finish(operation, AccountOperationStatus.Unavailable); }
    }

    private static bool Eligible(AccountUser? user, bool confirmation) => user is { IsActive: true, LockoutEnabled: true } &&
        (confirmation ? !user.EmailConfirmed : user.EmailConfirmed && !string.IsNullOrEmpty(user.PasswordHash));

    private static string MarkerName(bool confirmation) => confirmation ? "EmailConfirmation" : "PasswordReset";
    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool Matches(string? marker, string token)
    {
        if (marker is null || marker.Length != 64 || !marker.All(char.IsAsciiHexDigit)) { return false; }
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(marker), Convert.FromHexString(Digest(token)));
    }
    private static bool IsDependencyFailure(Exception exception) => exception is DbException or DbUpdateException or RedisException or TimeoutException
        or InvalidOperationException { InnerException: DbException };

    private AccountOperationResult Finish(AccountRateOperation operation, AccountOperationStatus status)
    {
        logger.LogInformation("Account recovery operation {Operation} completed with outcome {Outcome}", operation, status);
        return new(status, ThrottleSeconds: status == AccountOperationStatus.Throttled ? 900 : 60);
    }
}
