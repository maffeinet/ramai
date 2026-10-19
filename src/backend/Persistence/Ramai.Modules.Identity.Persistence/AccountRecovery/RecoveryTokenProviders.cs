using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ramai.Modules.Identity.Persistence.AccountRecovery;

public sealed class ConfirmationTokenOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "Ramai.EmailConfirmation.v1";
    public ConfirmationTokenOptions() { Name = ProviderName; TokenLifespan = TimeSpan.FromHours(24); }
}

public sealed class ResetTokenOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "Ramai.PasswordReset.v1";
    public ResetTokenOptions() { Name = ProviderName; TokenLifespan = TimeSpan.FromMinutes(30); }
}

public sealed class ConfirmationTokenProvider(IDataProtectionProvider protection, IOptions<ConfirmationTokenOptions> options,
    ILogger<DataProtectorTokenProvider<AccountUser>> logger)
    : DataProtectorTokenProvider<AccountUser>(protection, options, logger);

public sealed class ResetTokenProvider(IDataProtectionProvider protection, IOptions<ResetTokenOptions> options,
    ILogger<DataProtectorTokenProvider<AccountUser>> logger)
    : DataProtectorTokenProvider<AccountUser>(protection, options, logger);
