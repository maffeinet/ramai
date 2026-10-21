using System.Net;
using System.Security.Claims;

namespace Ramai.Application.Contracts.Identity;

// Classes intentionally do not format secrets through generated record ToString().
public sealed class MfaDecision(LoginOutcome outcome, string? secret = null,
    IReadOnlyList<string>? recoveryCodes = null, string? stamp = null, string? recoveryGrant = null)
{
    public LoginOutcome Outcome { get; } = outcome;
    public string? Secret { get; } = secret;
    public IReadOnlyList<string>? RecoveryCodes { get; } = recoveryCodes;
    public string? Stamp { get; } = stamp;
    public string? RecoveryGrant { get; } = recoveryGrant;
}

public enum MfaOperation { BeginEnrollment, ConfirmEnrollment, RegenerateRecoveryCodes }

public interface IAccountMfaService
{
    Task<MfaDecision> ManageAsync(ClaimsPrincipal principal, MfaOperation operation, string password,
        string? code, string? recoveryCode, IPAddress origin, CancellationToken cancellationToken = default);
    Task<MfaDecision> VerifyLoginAsync(Guid userId, string stamp, string? code, string? recoveryCode,
        IPAddress origin, CancellationToken cancellationToken = default);
}
