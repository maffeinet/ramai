using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Ramai.Application.Contracts.Identity;
using StackExchange.Redis;

namespace Ramai.Modules.Identity.Persistence.Sessions;

public sealed class RedisSessionTicketStore : ITicketStore
{
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Absolute = TimeSpan.FromSeconds(AccountSessionClaims.MaximumAgeSeconds);
    private readonly IDatabase _database;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;
    private readonly string _prefix;

    public RedisSessionTicketStore(IConnectionMultiplexer redis, IDataProtectionProvider protection,
        TimeProvider clock, string sessionNamespace)
    {
        if (string.IsNullOrEmpty(sessionNamespace) || sessionNamespace.Length > 80 ||
            !sessionNamespace.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new ArgumentException("Invalid session namespace.", nameof(sessionNamespace));
        }
        _database = redis.GetDatabase();
        _protector = protection.CreateProtector("Ramai.SessionTickets.v1", sessionNamespace);
        _clock = clock;
        _prefix = $"ramai:{{{sessionNamespace}}}:session:";
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var ttl = Lifetime(ticket);
        if (ttl <= TimeSpan.Zero || !await _database.StringSetAsync(_prefix + key,
            Encode(ticket), ttl, When.NotExists))
        {
            throw new InvalidOperationException("Session could not be stored.");
        }
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!ValidKey(key)) { return; }
        var ttl = Lifetime(ticket);
        if (ttl <= TimeSpan.Zero) { await RemoveAsync(key); return; }
        // XX is atomic: a concurrent logout/expiry cannot be resurrected by renewal.
        await _database.StringSetAsync(_prefix + key, Encode(ticket), ttl, When.Exists);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!ValidKey(key)) { return null; }
        var value = await _database.StringGetAsync(_prefix + key);
        if (value.IsNull) { return null; }
        try
        {
            var ticket = TicketSerializer.Default.Deserialize(_protector.Unprotect((byte[])value!));
            return ticket is not null && Lifetime(ticket) > TimeSpan.Zero ? ticket : null;
        }
        catch (CryptographicException) { return null; }
    }

    public async Task RemoveAsync(string key)
    {
        if (ValidKey(key)) { await _database.KeyDeleteAsync(_prefix + key); }
    }

    private byte[] Encode(AuthenticationTicket ticket) => _protector.Protect(TicketSerializer.Default.Serialize(ticket));
    private static bool ValidKey(string key) => key.Length == 64 && key.All(char.IsAsciiHexDigit);
    private TimeSpan Lifetime(AuthenticationTicket ticket)
    {
        if (!long.TryParse(ticket.Principal.FindFirst(AccountSessionClaims.Started)?.Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) { return TimeSpan.Zero; }
        // Validate numeric bounds without letting malformed server state crash the host.
        if (seconds < 0 || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) { return TimeSpan.Zero; }
        var start = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var now = _clock.GetUtcNow();
        if (start > now || now - start >= Absolute) { return TimeSpan.Zero; }
        var expires = ticket.Properties.ExpiresUtc ?? now;
        var absoluteLeft = Absolute - (now - start);
        var idleLeft = expires - now;
        return idleLeft < absoluteLeft ? idleLeft : absoluteLeft;
    }
}
