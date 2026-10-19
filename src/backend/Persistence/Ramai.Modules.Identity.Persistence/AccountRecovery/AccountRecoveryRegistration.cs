using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ramai.Modules.Identity.Persistence.AccountRecovery;

public static class AccountRecoveryRegistration
{
    // Requires AddRamaiAccountSecurity first. No default keyring, ephemeral provider,
    // SMTP sender, token console output, auth scheme or session registration.
    public static IServiceCollection AddRamaiAccountRecovery(this IServiceCollection services,
        IDataProtectionProvider protection, IAccountRecoverySender sender, Uri publicOrigin)
    {
        ArgumentNullException.ThrowIfNull(protection);
        ArgumentNullException.ThrowIfNull(sender);
        services.AddSingleton(protection);
        services.AddSingleton(sender);
        services.AddSingleton(new RecoveryLinks(publicOrigin));
        services.AddOptions<ConfirmationTokenOptions>();
        services.AddOptions<ResetTokenOptions>();
        new IdentityBuilder(typeof(AccountUser), services)
            .AddTokenProvider<ConfirmationTokenProvider>(ConfirmationTokenOptions.ProviderName)
            .AddTokenProvider<ResetTokenProvider>(ResetTokenOptions.ProviderName);
        services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.EmailConfirmationTokenProvider = ConfirmationTokenOptions.ProviderName;
            options.Tokens.PasswordResetTokenProvider = ResetTokenOptions.ProviderName;
        });
        services.AddScoped<AccountRecoveryService>();
        return services;
    }
}
