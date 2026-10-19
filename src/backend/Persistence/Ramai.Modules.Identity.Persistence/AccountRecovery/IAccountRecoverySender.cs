namespace Ramai.Modules.Identity.Persistence.AccountRecovery;

public enum RecoveryPurpose { EmailConfirmation, PasswordReset }

// No record-generated ToString: recipient and bearer URL must never enter logs.
public sealed class AccountRecoveryMessage(string recipient, RecoveryPurpose purpose, Uri link)
{
    public string Recipient { get; } = recipient;
    public RecoveryPurpose Purpose { get; } = purpose;
    public Uri Link { get; } = link;
    public override string ToString() => nameof(AccountRecoveryMessage);
}

// An internal delivery boundary, never a public API. Test implementation lives only in tests.
public interface IAccountRecoverySender
{
    Task SendAsync(AccountRecoveryMessage message, CancellationToken cancellationToken);
}
