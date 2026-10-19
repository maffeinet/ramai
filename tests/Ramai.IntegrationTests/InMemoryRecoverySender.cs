using System.Collections.Concurrent;
using Ramai.Modules.Identity.Persistence.AccountRecovery;

namespace Ramai.IntegrationTests;

// Synthetic tests only. Never registered by the production host, exposed as an API,
// or written to console/logs/files. Caller must not dump Messages to test output.
internal sealed class InMemoryRecoverySender : IAccountRecoverySender
{
    public ConcurrentQueue<AccountRecoveryMessage> Messages { get; } = new();
    public bool FailDelivery { get; set; }
    public Task SendAsync(AccountRecoveryMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailDelivery) { throw new InvalidOperationException("Synthetic sender failure: " + message.Link); }
        Messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
