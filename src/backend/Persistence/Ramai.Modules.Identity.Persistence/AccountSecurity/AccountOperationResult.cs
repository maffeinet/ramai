namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public enum AccountOperationStatus { Accepted, Rejected, Throttled, Unavailable }

// Accepted credential check is NOT a principal/session, MFA completion or tenant grant.
public sealed record AccountOperationResult(AccountOperationStatus Status, Guid? UserId = null, int ThrottleSeconds = 60)
{
    public string Message => Status switch
    {
        AccountOperationStatus.Accepted => "Request processed.",
        AccountOperationStatus.Throttled => "Too many requests. Retry later.",
        AccountOperationStatus.Unavailable => "Service temporarily unavailable.",
        _ => "Unable to process the request with the supplied credentials."
    };
    public TimeSpan? RetryAfter => Status == AccountOperationStatus.Throttled ? TimeSpan.FromSeconds(ThrottleSeconds) : null;
}
