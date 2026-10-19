using System.Net;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public sealed class AccountRateLimiter : IDisposable
{
    private readonly IDatabase _database;
    private readonly byte[] _key;
    private readonly string _prefix;
    private const string Script = """
        for i = 1, 3 do
            local count = redis.call('INCR', KEYS[i])
            if count == 1 then redis.call('EXPIRE', KEYS[i], ARGV[i + 3]) end
            if count > tonumber(ARGV[i]) then return 0 end
        end
        return 1
        """;

    public AccountRateLimiter(IConnectionMultiplexer redis, byte[] key, string keyNamespace)
    {
        if (key.Length < 32) { throw new ArgumentException("Rate key must contain at least 32 random bytes.", nameof(key)); }
        if (string.IsNullOrEmpty(keyNamespace) || keyNamespace.Length > 64 || !keyNamespace.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new ArgumentException("Invalid rate namespace.", nameof(keyNamespace));
        }
        _key = key.ToArray();
        _database = redis.GetDatabase();
        _prefix = $"ramai:{{{keyNamespace}}}:";
    }

    public Task<bool> AllowAsync(bool registration, string normalizedEmail, IPAddress origin, CancellationToken cancellationToken) =>
        AllowAsync(registration ? AccountRateOperation.Register : AccountRateOperation.CheckCredentials, normalizedEmail, origin, cancellationToken);

    public async Task<bool> AllowAsync(AccountRateOperation operation, string accountKey, IPAddress origin, CancellationToken cancellationToken)
    {
        var name = operation switch
        {
            AccountRateOperation.Register => "register",
            AccountRateOperation.CheckCredentials => "verify",
            AccountRateOperation.RequestEmailConfirmation => "request-confirmation",
            AccountRateOperation.RequestPasswordReset => "request-reset",
            AccountRateOperation.ConfirmEmail => "confirm-email",
            AccountRateOperation.ResetPassword => "reset-password",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var limits = operation switch
        {
            AccountRateOperation.Register or AccountRateOperation.CheckCredentials => new RedisValue[] { 300, 120, 10, 60, 60, 60 },
            AccountRateOperation.RequestEmailConfirmation or AccountRateOperation.RequestPasswordReset => [60, 20, 3, 60, 60, 900],
            _ => [120, 60, 5, 60, 60, 900]
        };
        RedisKey[] keys = [_prefix + name, _prefix + name + ":origin:" + Digest(origin.ToString()),
            _prefix + name + ":account:" + Digest(accountKey)];
        var result = await _database.ScriptEvaluateAsync(Script, keys, limits).WaitAsync(cancellationToken);
        return (int)result == 1;
    }

    private string Digest(string value) => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));
    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
