using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ramai.IntegrationTests;

internal sealed class SessionTestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}

internal sealed class WebSessionFixture : IDisposable
{
    public AccountSecurityFixture Accounts { get; }
    public SessionTestClock Clock { get; } = new();
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "ramai-session-test-" + Guid.NewGuid().ToString("N"));
    private readonly string _certificatePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly List<WebApplicationFactory<Program>> _hosts = [];
    private readonly List<X509Certificate2> _serverCertificates = [];
    public const string Origin = "https://ramai.example.invalid";

    public WebSessionFixture(AccountSecurityFixture accounts)
    {
        Accounts = accounts;
        Directory.CreateDirectory(DirectoryPath);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=RAMAI synthetic session test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(Path.Combine(DirectoryPath, "keys.pfx"), certificate.Export(X509ContentType.Pfx, _certificatePassword));
    }

    public WebApplicationFactory<Program> CreateHost(string? databaseOverride = null, bool foreignKeyring = false,
        string? redisPortOverride = null)
    {
        var connection = new NpgsqlConnectionStringBuilder(Accounts.Connection);
        var settings = new Dictionary<string, string?>
        {
            ["RAMAI_AUTH_ENABLED"] = "true",
            ["RAMAI_AUTH_ORIGIN"] = Origin,
            ["RAMAI_AUTH_KEY_DIRECTORY"] = Path.Combine(DirectoryPath, foreignKeyring ? "foreign-ring" : "ring"),
            ["RAMAI_AUTH_CERTIFICATE_PATH"] = Path.Combine(DirectoryPath, "keys.pfx"),
            ["RAMAI_AUTH_CERTIFICATE_PASSWORD"] = _certificatePassword,
            ["RAMAI_AUTH_RATE_KEY"] = Convert.ToBase64String(Accounts.RateKey),
            ["RAMAI_AUTH_NAMESPACE"] = Accounts.RateNamespace,
            ["RAMAI_DB_HOST"] = connection.Host,
            ["RAMAI_DB_PORT"] = connection.Port.ToString(),
            ["RAMAI_DB_NAME"] = databaseOverride ?? connection.Database,
            ["RAMAI_DB_USER"] = connection.Username,
            ["RAMAI_DB_PASSWORD"] = connection.Password,
            ["RAMAI_REDIS_HOST"] = Environment.GetEnvironmentVariable("RAMAI_REDIS_HOST") ?? "localhost",
            ["RAMAI_REDIS_PORT"] = redisPortOverride ?? Environment.GetEnvironmentVariable("RAMAI_REDIS_PORT") ?? "6379"
        };
        var factory = new SessionApiFactory(settings, Clock, Accounts.Logs);
        _hosts.Add(factory);
        return factory;
    }

    public static HttpClient Client(WebApplicationFactory<Program> host, bool cookies = true) => host.CreateClient(new()
    {
        BaseAddress = new Uri(Origin),
        AllowAutoRedirect = false,
        HandleCookies = cookies
    });

    public HttpClient NetworkClient()
    {
        // Windows SChannel cannot serve TLS with an EphemeralKeySet imported PFX.
        // DefaultKeySet is disposed with the test; no certificate/trust-store install.
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(DirectoryPath, "keys.pfx"),
            _certificatePassword, X509KeyStorageFlags.DefaultKeySet);
        _serverCertificates.Add(certificate);
        var host = CreateHost();
        host.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, endpoint => endpoint.UseHttps(certificate)));
        host.StartServer();
        var server = host.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
        var address = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.Single();
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            // Only this freshly generated self-signed TEST certificate is trusted;
            // never disable certificate/hostname verification in application code.
            ServerCertificateCustomValidationCallback = (_, presented, _, errors) =>
                presented?.Thumbprint == certificate.Thumbprint &&
                (errors == System.Net.Security.SslPolicyErrors.None || errors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
        };
        return new HttpClient(handler) { BaseAddress = new Uri(address) };
    }

    public static async Task<string> Csrf(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/account/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("requestToken").GetString()!;
    }

    public static async Task<HttpResponseMessage> Write(HttpClient client, string operation, object? body = null,
        string? token = null, string? origin = null)
    {
        token ??= await Csrf(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/" + operation);
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Origin", origin ?? Origin);
        request.Headers.Add("X-Correlation-ID", "session-http-check");
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> Login(HttpClient client) =>
        await Write(client, "login", new { email = Accounts.Email, password = Accounts.Password });

    public static string SessionCookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Single(value => value.StartsWith("__Host-Ramai.Session=", StringComparison.Ordinal)).Split(';')[0];

    public void Dispose()
    {
        foreach (var host in _hosts) { host.Dispose(); }
        foreach (var certificate in _serverCertificates) { certificate.Dispose(); }
        var resolved = Path.GetFullPath(DirectoryPath);
        if (Path.GetDirectoryName(resolved) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
            || !Path.GetFileName(resolved).StartsWith("ramai-session-test-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsafe test keyring cleanup.");
        }
        Directory.Delete(resolved, true);
    }

    private sealed class SessionApiFactory(Dictionary<string, string?> settings, SessionTestClock clock,
        CaptureAccountLogs logs) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Minimal hosting reads the feature switch before Build. Supply host
            // settings early, not a late ConfigureAppConfiguration callback.
            foreach (var setting in settings) { builder.UseSetting(setting.Key, setting.Value); }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IStartupFilter, ConnectionMetadata>();
                services.AddLogging(logging => logging.AddProvider(logs));
            });
        }
    }

    private sealed class ConnectionMetadata : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Real auth pipeline; only TestServer's absent socket metadata is supplied.
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.42");
                return continuation(context);
            });
            next(app);
        };
    }
}
