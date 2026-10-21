using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ramai.Application.Contracts.Identity;
using Ramai.Infrastructure.Configuration;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Ramai.Modules.Identity.Persistence.Sessions;
using StackExchange.Redis;

namespace Ramai.Api.Composition;

// Composition root only. HTTP handlers must use application contracts, never EF/UserManager.
public static class WebIdentityRegistration
{
    public static bool AddConfiguredWebIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("RAMAI_AUTH_ENABLED")) { return false; }
        // No implicit keyring or reusable development secret when auth is enabled.
        var keyDirectory = Required(configuration, "RAMAI_AUTH_KEY_DIRECTORY");
        var certificatePath = Required(configuration, "RAMAI_AUTH_CERTIFICATE_PATH");
        if (!Path.IsPathFullyQualified(keyDirectory) || !Path.IsPathFullyQualified(certificatePath))
        {
            throw new InvalidOperationException("Auth key paths must be absolute.");
        }
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
            Required(configuration, "RAMAI_AUTH_CERTIFICATE_PASSWORD"), X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
            || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        {
            certificate.Dispose();
            throw new InvalidOperationException("Auth key certificate is not usable.");
        }
        var rateKey = Convert.FromBase64String(Required(configuration, "RAMAI_AUTH_RATE_KEY"));
        if (rateKey.Length < 32) { throw new InvalidOperationException("Auth rate key must contain at least 32 random bytes."); }
        var origin = new Uri(Required(configuration, "RAMAI_AUTH_ORIGIN"), UriKind.Absolute);
        if (origin.Scheme != "https" || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/"
            || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            throw new InvalidOperationException("Auth origin must be an HTTPS origin without path or credentials.");
        }
        var sessionNamespace = Required(configuration, "RAMAI_AUTH_NAMESPACE");
        if (sessionNamespace.Length > 58 || !sessionNamespace.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new InvalidOperationException("Auth namespace must contain 1-58 ASCII letters/digits/hyphens.");
        }
        services.AddSingleton(new WebIdentityOrigin(origin.GetLeftPart(UriPartial.Authority)));
        services.AddSingleton(certificate);
        services.AddDataProtection().SetApplicationName("RAMAI.WebIdentity.v1:" + sessionNamespace)
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory)).ProtectKeysWithCertificate(certificate);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var endpoint = provider.GetRequiredService<RedisOptions>();
            var options = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                ConnectTimeout = 1000,
                AsyncTimeout = 1000,
                SyncTimeout = 1000,
                ConnectRetry = 0
            };
            options.EndPoints.Add(endpoint.Host, endpoint.Port);
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddRamaiAccountSecurity((providerOptions) => providerOptions.UseNpgsql(
            ConnectionString(configuration), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")),
            provider => provider.GetRequiredService<IConnectionMultiplexer>(), rateKey, sessionNamespace + "-rates");
        services.AddScoped<IAccountLoginService, AccountLoginService>();
        services.AddScoped<IAccountSessionValidator, AccountSessionValidator>();
        services.AddScoped<IUserAccountReader, UserAccountReader>();
        services.AddScoped<IUserStore<AccountUser>, ProtectedIdentityTokenStore>();
        services.AddScoped<IAccountMfaService, AccountMfaService>();
        new IdentityBuilder(typeof(AccountUser), services)
            .AddTokenProvider<AuthenticatorTokenProvider<AccountUser>>(TokenOptions.DefaultAuthenticatorProvider);
        services.AddScoped<SessionCookieEvents>();
        services.AddSingleton<RedisSessionTicketStore>(provider => new(
            provider.GetRequiredService<IConnectionMultiplexer>(), provider.GetRequiredService<IDataProtectionProvider>(),
            provider.GetRequiredService<TimeProvider>(), sessionNamespace));
        services.AddAuthentication(AccountSessionClaims.Scheme).AddCookie(AccountSessionClaims.Scheme, options =>
        {
            options.Cookie.Name = "__Host-Ramai.Session";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = RedisSessionTicketStore.Idle;
            options.SlidingExpiration = true;
            options.EventsType = typeof(SessionCookieEvents);
        });
        services.AddOptions<CookieAuthenticationOptions>(AccountSessionClaims.Scheme)
            .Configure<RedisSessionTicketStore, TimeProvider>((options, store, clock) =>
            {
                options.SessionStore = store;
                options.TimeProvider = clock;
            });
        services.AddAuthorization();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-Ramai.Antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        return true;
    }

    private static string ConnectionString(IConfiguration configuration) => new PostgreSqlOptions(
        configuration["RAMAI_DB_HOST"] ?? "localhost", configuration.GetValue("RAMAI_DB_PORT", 5432),
        configuration["RAMAI_DB_NAME"] ?? "ramai_dev", configuration["RAMAI_DB_USER"] ?? "ramai",
        Required(configuration, "RAMAI_DB_PASSWORD")).ToConnectionString();

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]!
            : throw new InvalidOperationException($"Missing required auth configuration: {key}.");
}

public sealed record WebIdentityOrigin(string Value);
