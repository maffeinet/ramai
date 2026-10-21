using System.Net;
using System.Security.Claims;

namespace Ramai.Application.Contracts.Identity;

public enum LoginOutcome { Accepted, Rejected, Throttled, Unavailable }

// Not a record: never format principals or security markers into diagnostic text.
public sealed class LoginDecision(LoginOutcome outcome, ClaimsPrincipal? principal = null)
{
    public LoginOutcome Outcome { get; } = outcome;
    public ClaimsPrincipal? Principal { get; } = principal;
}

public interface IAccountLoginService
{
    Task<LoginDecision> CheckAsync(string email, string password, IPAddress origin, string correlationId,
        CancellationToken cancellationToken = default, string? code = null, string? recoveryCode = null);
}

public interface IAccountSessionValidator
{
    Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

public static class AccountSessionClaims
{
    public const long MaximumAgeSeconds = 12 * 60 * 60;
    public const string Scheme = "Ramai.Session";
    public const string SecurityStamp = "ramai.security_stamp";
    public const string Started = "ramai.session_started";
    public const string Mfa = "ramai.mfa";
    public const string RecoveryGrant = "ramai.recovery_grant";
}
