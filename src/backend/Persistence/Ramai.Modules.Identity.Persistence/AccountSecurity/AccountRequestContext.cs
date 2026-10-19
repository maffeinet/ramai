using System.Net;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

// Construct only from trusted server connection metadata, not a free client header.
public sealed class AccountRequestContext
{
    public AccountRequestContext(IPAddress origin, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (string.IsNullOrEmpty(correlationId) || correlationId.Length > 128 ||
            !correlationId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':'))
        {
            throw new ArgumentException("Invalid correlation ID.", nameof(correlationId));
        }
        Origin = origin.IsIPv4MappedToIPv6 ? origin.MapToIPv4() : origin;
        CorrelationId = correlationId;
    }
    public IPAddress Origin { get; }
    public string CorrelationId { get; }
}
