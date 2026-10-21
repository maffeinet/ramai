using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Identity.Persistence.AccountRecovery;
using StackExchange.Redis;

namespace Ramai.IntegrationTests;

internal sealed class AccountSecurityFixture : IAsyncDisposable
{
    private readonly NpgsqlConnectionStringBuilder _admin;
    private readonly string _database;
    public string Connection { get; }
    public IConnectionMultiplexer Redis { get; }
    public string RateNamespace { get; } = "test-" + Guid.NewGuid().ToString("N");
    public byte[] RateKey { get; } = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    public CaptureAccountLogs Logs { get; } = new();
    public IDataProtectionProvider Protection { get; } = new EphemeralDataProtectionProvider();
    public InMemoryRecoverySender Inbox { get; } = new();
    public ServiceProvider Services { get; }
    public string Email { get; } = $"synthetic-{Guid.NewGuid():N}@example.invalid";
    public string Password { get; } = "violet rivers carry quiet lanterns " + Guid.NewGuid().ToString("N");

    private AccountSecurityFixture(NpgsqlConnectionStringBuilder admin, string database, IConnectionMultiplexer redis, bool recovery)
    {
        _admin = admin;
        _database = database;
        var connection = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = database };
        Connection = connection.ConnectionString;
        Redis = redis;
        Services = BuildServices(Connection, Redis, recovery);
    }

    public ServiceProvider BuildServices(string connection, IConnectionMultiplexer redis, bool recovery = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(options => options.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));
        services.AddRamaiAccountSecurity(options => options.UseNpgsql(connection,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")), redis, RateKey, RateNamespace);
        if (recovery) { services.AddRamaiAccountRecovery(Protection, Inbox, new Uri("https://ramai.example.invalid")); }
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public static async Task<AccountSecurityFixture> CreateAsync(bool recovery = false)
    {
        var admin = new NpgsqlConnectionStringBuilder
        {
            Host = Environment.GetEnvironmentVariable("RAMAI_DB_HOST") ?? "localhost",
            Port = int.Parse(Environment.GetEnvironmentVariable("RAMAI_DB_PORT") ?? "5432"),
            Username = Environment.GetEnvironmentVariable("RAMAI_DB_USER") ?? "ramai",
            Password = Environment.GetEnvironmentVariable("RAMAI_DB_PASSWORD") ?? throw new InvalidOperationException("Test credentials required."),
            Database = "postgres",
            Pooling = false,
            IncludeErrorDetail = false
        };
        var database = "ramai_s1test_" + Guid.NewGuid().ToString("N");
        await Execute(admin.ConnectionString, $"CREATE DATABASE \"{database}\"");
        IConnectionMultiplexer? redis = null;
        AccountSecurityFixture? fixture = null;
        try
        {
            var options = new ConfigurationOptions { AbortOnConnectFail = false, ConnectRetry = 0, ConnectTimeout = 1000, AsyncTimeout = 1000 };
            options.EndPoints.Add(Environment.GetEnvironmentVariable("RAMAI_REDIS_HOST") ?? "localhost",
                int.Parse(Environment.GetEnvironmentVariable("RAMAI_REDIS_PORT") ?? "6379"));
            redis = await ConnectionMultiplexer.ConnectAsync(options);
            fixture = new AccountSecurityFixture(admin, database, redis, recovery);
            await using var scope = fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IdentityStoreContext>().Database.MigrateAsync();
            return fixture;
        }
        catch
        {
            if (fixture is not null) { await fixture.Services.DisposeAsync(); }
            redis?.Dispose();
            await Execute(admin.ConnectionString, $"DROP DATABASE \"{database}\" WITH (FORCE)");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Inbox.Messages.Clear();
        await Services.DisposeAsync();
        // Only keys in this test's generated namespace, never FLUSHDB/shared state.
        foreach (var endpoint in Redis.GetEndPoints())
        {
            foreach (var ownedNamespace in new[] { RateNamespace, RateNamespace + "-rates", RateNamespace + "-workspace" })
            {
                var keys = Redis.GetServer(endpoint).Keys(pattern: $"ramai:{{{ownedNamespace}}}:*").ToArray();
                if (keys.Length > 0) { await Redis.GetDatabase().KeyDeleteAsync(keys); }
            }
        }
        Redis.Dispose();
        await Execute(_admin.ConnectionString, $"DROP DATABASE \"{_database}\" WITH (FORCE)");
    }

    private static async Task Execute(string connection, string sql)
    {
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync();
        await using var command = new NpgsqlCommand(sql, database);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class CaptureAccountLogs : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();
    public ConcurrentQueue<string> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;
    public void Dispose() { }

    private sealed class CaptureLogger(CaptureAccountLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<string>();
            owner._scopes.ForEachScope((scope, list) => list.Add(scope is IEnumerable<KeyValuePair<string, object>> properties
                ? string.Join(",", properties.Select(pair => $"{pair.Key}={pair.Value}")) : scope?.ToString() ?? ""), scopes);
            owner.Entries.Enqueue($"{category}|{level}|{formatter(state, exception)}|{exception}|{string.Join(';', scopes)}");
        }
    }
}
