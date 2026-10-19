using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public static class AccountSecurityRegistration
{
    public const int HashIterations = 220_000;

    public static IServiceCollection AddRamaiAccountSecurity(this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDatabase, IConnectionMultiplexer redis, byte[] rateKey,
        string rateNamespace = "identity-rates")
    {
        services.AddLogging();
        services.AddDbContext<IdentityStoreContext>(options =>
        {
            configureDatabase(options);
            options.EnableSensitiveDataLogging(false);
        });
        services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Password.RequiredLength = 15;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<IdentityStoreContext>();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = HashIterations);
        services.RemoveAll<IPasswordValidator<AccountUser>>();
        services.AddSingleton<IPasswordValidator<AccountUser>, AccountPasswordValidator>();
        services.AddSingleton<IPasswordHasher<AccountUser>, NormalizedPasswordHasher>();
        services.AddSingleton<DummyPasswordWork>();
        services.AddSingleton(_ => new AccountRateLimiter(redis, rateKey, rateNamespace));
        services.AddScoped<AccountCredentialService>();
        return services;
    }
}
