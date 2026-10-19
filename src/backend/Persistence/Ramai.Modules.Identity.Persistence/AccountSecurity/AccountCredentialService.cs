using System.Data.Common;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public sealed class AccountCredentialService(UserManager<AccountUser> manager, IdentityStoreContext database,
    AccountRateLimiter limiter, DummyPasswordWork dummy, ILogger<AccountCredentialService> logger)
{
    public Task<AccountOperationResult> RegisterAsync(string email, string password, AccountRequestContext request,
        CancellationToken cancellationToken = default) => RunAsync(true, email, password, request, cancellationToken);

    public Task<AccountOperationResult> CheckCredentialsAsync(string email, string password, AccountRequestContext request,
        CancellationToken cancellationToken = default) => RunAsync(false, email, password, request, cancellationToken);

    private async Task<AccountOperationResult> RunAsync(bool registration, string email, string password,
        AccountRequestContext request, CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = request.CorrelationId });
        AccountOperationResult result;
        try
        {
            var trimmed = email?.Trim();
            if (trimmed is null || trimmed.Length > 256 || !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
            {
                return Finish(registration, new(AccountOperationStatus.Rejected));
            }
            var normalized = manager.NormalizeEmail(trimmed);
            if (!await limiter.AllowAsync(registration, normalized, request.Origin, cancellationToken))
            {
                return Finish(registration, new(AccountOperationStatus.Throttled));
            }
            if (registration && PasswordText.Normalize(password) is null)
            {
                dummy.Perform(password);
                return Finish(registration, new(AccountOperationStatus.Rejected));
            }
            result = registration ? await RegisterCore(trimmed, normalized, password, cancellationToken)
                : await CheckCore(normalized, password, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or RedisException or TimeoutException
            or InvalidOperationException { InnerException: DbException })
        {
            // No exception text/SQL/credentials. Dependency failures never grant access.
            result = new(AccountOperationStatus.Unavailable);
        }
        return Finish(registration, result);
    }

    private async Task<AccountOperationResult> RegisterCore(string email, string normalized, string password, CancellationToken cancellationToken)
    {
        var user = new AccountUser { Id = Guid.NewGuid(), Email = email, UserName = email, LockoutEnabled = true };
        foreach (var validator in manager.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(manager, user, password)).Succeeded)
            {
                return new(AccountOperationStatus.Rejected);
            }
        }
        if (await database.Users.AsNoTracking().AnyAsync(x => x.NormalizedEmail == normalized, cancellationToken))
        {
            dummy.Perform(password);
            return new(AccountOperationStatus.Accepted);
        }
        try
        {
            var result = await manager.CreateAsync(user, password);
            // Duplicate races must not reveal whether the account exists.
            return new(result.Succeeded || result.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName")
                ? AccountOperationStatus.Accepted : AccountOperationStatus.Rejected);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(AccountOperationStatus.Accepted);
        }
    }

    private async Task<AccountOperationResult> CheckCore(string normalized, string password, CancellationToken cancellationToken)
    {
        // Row lock serializes failure increments across instances without a new table,
        // preventing lost counters with the standard optimistic-concurrency store.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await database.Users.FromSqlInterpolated(
            $"SELECT * FROM identity.\"Users\" WHERE \"NormalizedEmail\" = {normalized} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (user is not null)
        {
            // A scoped context may already track an account from an earlier call;
            // refresh under the acquired row lock before evaluating counters/state.
            await database.Entry(user).ReloadAsync(cancellationToken);
        }
        if (user is null || !user.IsActive || !user.LockoutEnabled || string.IsNullOrEmpty(user.PasswordHash) || await manager.IsLockedOutAsync(user))
        {
            dummy.Perform(password);
            return new(AccountOperationStatus.Rejected);
        }
        var valid = false;
        if (PasswordText.Normalize(password) is null)
        {
            dummy.Perform(password);
        }
        else
        {
            valid = await manager.CheckPasswordAsync(user, password);
        }
        if (!valid)
        {
            if (!(await manager.AccessFailedAsync(user)).Succeeded)
            {
                return new(AccountOperationStatus.Rejected);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(AccountOperationStatus.Rejected);
        }
        if (!(await manager.ResetAccessFailedCountAsync(user)).Succeeded)
        {
            return new(AccountOperationStatus.Rejected);
        }
        await transaction.CommitAsync(cancellationToken);
        // Email confirmation/MFA/session flows are not implemented here. Unconfirmed
        // accounts cannot proceed; this result alone must never authorize a tenant.
        return user.EmailConfirmed ? new(AccountOperationStatus.Accepted, user.Id) : new(AccountOperationStatus.Rejected);
    }

    private AccountOperationResult Finish(bool registration, AccountOperationResult result)
    {
        logger.LogInformation("Account security operation {Operation} completed with outcome {Outcome}",
            registration ? "Register" : "CheckCredentials", result.Status);
        return result;
    }
}
