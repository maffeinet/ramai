using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Ramai.Api.Composition;
using Ramai.Infrastructure;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Tenants.Domain;
using Ramai.Modules.Tenants.Persistence;
using StackExchange.Redis;

// Explicit, isolated browser-test setup only. Never a registration/bootstrap endpoint.
if (Environment.GetEnvironmentVariable("RAMAI_UI_TESTS") != "1") { return 2; }
var root = Path.GetFullPath(Environment.GetEnvironmentVariable("RAMAI_UI_TEST_ROOT") ?? "");
if (Path.GetDirectoryName(root) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
    || !Path.GetFileName(root).StartsWith("ramai-ui-", StringComparison.Ordinal)) { return 2; }
var statePath = Path.Combine(root, "fixture.json");
var database = "ramai_ui_" + Guid.NewGuid().ToString("N");
var admin = new NpgsqlConnectionStringBuilder
{
    Host = Environment.GetEnvironmentVariable("RAMAI_DB_HOST") ?? "127.0.0.1",
    Port = int.Parse(Environment.GetEnvironmentVariable("RAMAI_DB_PORT") ?? "5432"),
    Username = Environment.GetEnvironmentVariable("RAMAI_DB_USER") ?? "ramai",
    Password = Environment.GetEnvironmentVariable("RAMAI_DB_PASSWORD") ?? throw new InvalidOperationException("UI test database credentials required."),
    Database = "postgres",
    Pooling = false,
    IncludeErrorDetail = false
};
async Task Execute(string sql)
{
    await using var connection = new NpgsqlConnection(admin.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
void PrivateFile(string path)
{
    if (!OperatingSystem.IsWindows()) { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
}
try
{
    if (args is ["cleanup"])
    {
        if (!File.Exists(statePath)) { return 0; }
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(statePath));
        var config = state.RootElement.GetProperty("configuration");
        database = config.GetProperty("RAMAI_DB_NAME").GetString()!;
        var suffix = database.StartsWith("ramai_ui_", StringComparison.Ordinal) ? database[9..] : "";
        if (!Guid.TryParseExact(suffix, "N", out _)) { return 2; }
        var ns = config.GetProperty("RAMAI_AUTH_NAMESPACE").GetString()!;
        if (!ns.StartsWith("ui-", StringComparison.Ordinal) || !Guid.TryParseExact(ns[3..], "N", out _)) { return 2; }
        var options = new ConfigurationOptions { AbortOnConnectFail = false, ConnectTimeout = 1000, AsyncTimeout = 1000 };
        options.EndPoints.Add(config.GetProperty("RAMAI_REDIS_HOST").GetString()!, int.Parse(config.GetProperty("RAMAI_REDIS_PORT").GetString()!));
        using var redis = await ConnectionMultiplexer.ConnectAsync(options);
        foreach (var endpoint in redis.GetEndPoints())
        {
            foreach (var owned in new[] { ns, ns + "-rates", ns + "-workspace" })
            {
                var keys = redis.GetServer(endpoint).Keys(pattern: $"ramai:{{{owned}}}:*").ToArray();
                if (keys.Length > 0) { await redis.GetDatabase().KeyDeleteAsync(keys); }
            }
        }
        await Execute($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        Console.WriteLine("Isolated UI fixture removed.");
        return 0;
    }
    if (args is not ["prepare"] || File.Exists(statePath)) { return 2; }
    Directory.CreateDirectory(root);
    var certPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=RAMAI isolated UI test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var names = new SubjectAlternativeNameBuilder(); names.AddIpAddress(IPAddress.Loopback); names.AddDnsName("localhost");
    request.CertificateExtensions.Add(names.Build());
    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    var pfx = Path.Combine(root, "test.pfx"); var pem = Path.Combine(root, "test.pem"); var key = Path.Combine(root, "test.key");
    await File.WriteAllBytesAsync(pfx, certificate.Export(X509ContentType.Pfx, certPassword));
    await File.WriteAllTextAsync(pem, certificate.ExportCertificatePem());
    await File.WriteAllTextAsync(key, rsa.ExportPkcs8PrivateKeyPem());
    foreach (var path in new[] { pfx, pem, key }) { PrivateFile(path); }
    var configuration = new Dictionary<string, string?>
    {
        ["RAMAI_AUTH_ENABLED"] = "true",
        ["RAMAI_AUTH_ORIGIN"] = Environment.GetEnvironmentVariable("RAMAI_UI_ORIGIN"),
        ["RAMAI_AUTH_KEY_DIRECTORY"] = Path.Combine(root, "keyring"),
        ["RAMAI_AUTH_CERTIFICATE_PATH"] = pfx,
        ["RAMAI_AUTH_CERTIFICATE_PASSWORD"] = certPassword,
        ["RAMAI_AUTH_RATE_KEY"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["RAMAI_AUTH_NAMESPACE"] = "ui-" + Guid.NewGuid().ToString("N"),
        ["RAMAI_DB_HOST"] = admin.Host,
        ["RAMAI_DB_PORT"] = admin.Port.ToString(),
        ["RAMAI_DB_USER"] = admin.Username,
        ["RAMAI_DB_PASSWORD"] = admin.Password,
        ["RAMAI_DB_NAME"] = database,
        ["RAMAI_REDIS_HOST"] = Environment.GetEnvironmentVariable("RAMAI_REDIS_HOST") ?? "127.0.0.1",
        ["RAMAI_REDIS_PORT"] = Environment.GetEnvironmentVariable("RAMAI_REDIS_PORT") ?? "6379"
    };
    await Execute($"CREATE DATABASE \"{database}\"");
    try
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configuration).Build();
        var services = new ServiceCollection(); services.AddSingleton<IConfiguration>(config);
        services.AddLogging(logging => logging.ClearProviders()); services.AddRamaiInfrastructure(config);
        services.AddConfiguredWebIdentity(config); services.AddConfiguredWebTenants(config);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityStoreContext>();
        var tenants = scope.ServiceProvider.GetRequiredService<TenantsStoreContext>();
        await identity.Database.MigrateAsync(); await tenants.Database.MigrateAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var accounts = new Dictionary<string, object>();
        var users = new Dictionary<string, AccountUser>();
        foreach (var label in new[] { "member", "onboarding", "privileged", "outsider", "lifecycle" })
        {
            var email = $"{label}-{Guid.NewGuid():N}@example.invalid";
            var password = "quiet rivers carry violet lanterns " + Guid.NewGuid().ToString("N");
            var user = new AccountUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, IsActive = true };
            if (!(await manager.CreateAsync(user, password)).Succeeded) { throw new InvalidOperationException("Synthetic account setup failed."); }
            string? sharedKey = null;
            if (label == "privileged")
            {
                if (!(await manager.ResetAuthenticatorKeyAsync(user)).Succeeded || !(await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded)
                { throw new InvalidOperationException("Synthetic MFA setup failed."); }
                sharedKey = await manager.GetAuthenticatorKeyAsync(user);
            }
            users[label] = user; accounts[label] = new { email, password, sharedKey };
        }
        var a = Tenant.Create(Guid.NewGuid(), "Alfa Demo", users["privileged"].Id); a.AddMember(users["member"].Id);
        var b = Tenant.Create(Guid.NewGuid(), "Beta Demo", users["outsider"].Id); b.AddMember(users["privileged"].Id);
        tenants.Tenants.AddRange(a, b); await tenants.SaveChangesAsync();
        var state = new { configuration, accounts, workspaces = new { a = a.TenantId, b = b.TenantId }, certificate = pem, privateKey = key, pfx };
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state)); PrivateFile(statePath);
        Console.WriteLine("Isolated UI fixture prepared; credentials remain in private temporary files.");
    }
    catch { await Execute($"DROP DATABASE \"{database}\" WITH (FORCE)"); throw; }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("UI test fixture failed: " + error.GetType().Name + ". No credentials logged.");
    return 1;
}
