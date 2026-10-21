using System.Net;
using System.Security.Cryptography;
using System.Text;
using Ramai.Api.Workspaces;
using StackExchange.Redis;

namespace Ramai.Api.Composition;

// HTTP anti-abuse only; does not grant account or tenant permissions.
public sealed class WorkspaceRequestBudget : IWorkspaceRequestBudget, IDisposable
{
    private readonly IDatabase _database;
    private readonly byte[] _key;
    private readonly string _prefix;
    private const string Script = """
        local allowed = 1
        for i = 1, 3 do
            local count = redis.call('INCR', KEYS[i])
            if count == 1 then redis.call('EXPIRE', KEYS[i], 60) end
            if count > tonumber(ARGV[i]) then allowed = 0 end
        end
        return allowed
        """;

    public WorkspaceRequestBudget(IConnectionMultiplexer redis, IConfiguration configuration)
    {
        _database = redis.GetDatabase();
        // These are already required/validated by the opt-in identity composition.
        _key = Convert.FromBase64String(configuration["RAMAI_AUTH_RATE_KEY"]!);
        _prefix = "ramai:{" + configuration["RAMAI_AUTH_NAMESPACE"] + "-workspace}:create:";
    }

    public async Task<bool> AllowProvisioningAsync(Guid userId, IPAddress origin, CancellationToken cancellationToken)
    {
        RedisKey[] keys = [_prefix + "global", _prefix + "origin:" + Digest(origin.ToString()),
            _prefix + "user:" + Digest(userId.ToString("D"))];
        var result = await _database.ScriptEvaluateAsync(Script, keys, [300, 60, 10]).WaitAsync(cancellationToken);
        return (int)result == 1;
    }
    private string Digest(string value) => Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));
    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
