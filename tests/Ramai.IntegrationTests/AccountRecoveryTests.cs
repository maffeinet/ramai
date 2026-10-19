using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountRecovery;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using StackExchange.Redis;

namespace Ramai.IntegrationTests;

public sealed class AccountRecoveryTests
{
    private static AccountRequestContext Request(int origin = 1, string? correlation = null) =>
        new(IPAddress.Parse($"192.0.2.{origin}"), correlation ?? Guid.NewGuid().ToString("N"));
    private static string Code(AccountRecoveryMessage message) => QueryHelpers.ParseQuery(message.Link.Query)["code"].ToString();
    private static string ReplacementPassword() => "silver gardens welcome quiet birds " + Guid.NewGuid().ToString("N");

    private static async Task<AccountUser> Register(AccountSecurityFixture fixture, IServiceProvider services, bool confirmed = false)
    {
        var result = await services.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password, Request());
        Assert.Equal(AccountOperationStatus.Accepted, result.Status);
        var manager = services.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        if (confirmed) { user.EmailConfirmed = true; Assert.True((await manager.UpdateAsync(user)).Succeeded); }
        return user;
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Confirmation_uses_only_the_latest_link_once_and_never_issues_a_session()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider);
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        Assert.Equal(AccountOperationStatus.Accepted, (await recovery.RequestConfirmationAsync(fixture.Email, "/workspaces", Request())).Status);
        Assert.False(user.EmailConfirmed);
        var first = Assert.Single(fixture.Inbox.Messages);
        await recovery.RequestConfirmationAsync(fixture.Email, "/", Request());
        var last = fixture.Inbox.Messages.ToArray()[1];
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(user.Id, Code(first), Request())).Status);
        Assert.Equal(AccountOperationStatus.Accepted, (await recovery.ConfirmEmailAsync(user.Id, Code(last), Request())).Status);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(user.Id, Code(last), Request())).Status);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IdentityStoreContext>().UserTokens.ToListAsync());
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(2, options.Tokens.ProviderMap.Count);
        Assert.Equal(TimeSpan.FromHours(24), scope.ServiceProvider.GetRequiredService<IOptions<ConfirmationTokenOptions>>().Value.TokenLifespan);
        Assert.Equal(TimeSpan.FromMinutes(30), scope.ServiceProvider.GetRequiredService<IOptions<ResetTokenOptions>>().Value.TokenLifespan);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ConfirmationTokenProvider>().CanGenerateTwoFactorTokenAsync(manager, user));
        Assert.False(await scope.ServiceProvider.GetRequiredService<ResetTokenProvider>().CanGenerateTwoFactorTokenAsync(manager, user));
        Assert.Null(scope.ServiceProvider.GetService<SignInManager<AccountUser>>());
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Reset_is_single_use_changes_stamp_and_password_and_enforces_password_policy()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider, confirmed: true);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        await recovery.RequestResetAsync(fixture.Email, "/", Request());
        var message = Assert.Single(fixture.Inbox.Messages);
        var oldStamp = user.SecurityStamp;
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(user.Id, Code(message), null!, Request())).Status);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(user.Id, Code(message), "short", Request())).Status);
        var replacement = ReplacementPassword();
        Assert.Equal(AccountOperationStatus.Accepted, (await recovery.ResetPasswordAsync(user.Id, Code(message), replacement, Request())).Status);
        Assert.True(user.SecurityStamp != oldStamp);
        Assert.False(await manager.VerifyUserTokenAsync(user, ResetTokenOptions.ProviderName,
            UserManager<AccountUser>.ResetPasswordTokenPurpose, RecoveryLinks.DecodeCode(Code(message))!));
        Assert.True(await manager.CheckPasswordAsync(user, replacement));
        Assert.False(await manager.CheckPasswordAsync(user, fixture.Password));
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(user.Id, Code(message), ReplacementPassword(), Request())).Status);
        Assert.True(user.EmailConfirmed);
    }

    [InfrastructureTheory]
    [InlineData(true, 1380, true)]
    [InlineData(true, 1500, false)]
    [InlineData(false, 29, true)]
    [InlineData(false, 31, false)]
    [Trait("Category", "Infrastructure")]
    public async Task Standard_provider_honors_independent_24_hour_and_30_minute_expiry(bool confirmation, int minutes, bool accepted)
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider, confirmed: !confirmation);
        var code = await SeedStandardTokenAt(fixture, scope.ServiceProvider, user, confirmation, DateTimeOffset.UtcNow.AddMinutes(-minutes));
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        var result = confirmation ? await recovery.ConfirmEmailAsync(user.Id, code, Request()) :
            await recovery.ResetPasswordAsync(user.Id, code, ReplacementPassword(), Request());
        Assert.Equal(accepted ? AccountOperationStatus.Accepted : AccountOperationStatus.Rejected, result.Status);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Tokens_are_user_purpose_and_keyring_bound_and_tampering_is_rejected()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider);
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        await recovery.RequestConfirmationAsync(fixture.Email, "/", Request());
        var message = Assert.Single(fixture.Inbox.Messages);
        var token = RecoveryLinks.DecodeCode(Code(message))!;
        Assert.False(await manager.VerifyUserTokenAsync(user, ResetTokenOptions.ProviderName, UserManager<AccountUser>.ResetPasswordTokenPurpose, token));
        Assert.False(await manager.VerifyUserTokenAsync(user, ConfirmationTokenOptions.ProviderName, "OtherPurpose", token));
        var foreignUser = new AccountUser { Id = Guid.NewGuid(), SecurityStamp = user.SecurityStamp };
        Assert.False(await manager.VerifyUserTokenAsync(foreignUser, ConfirmationTokenOptions.ProviderName, UserManager<AccountUser>.ConfirmEmailTokenPurpose, token));
        var foreignProvider = new ConfirmationTokenProvider(new EphemeralDataProtectionProvider(), Options.Create(new ConfirmationTokenOptions()),
            NullLogger<DataProtectorTokenProvider<AccountUser>>.Instance);
        Assert.False(await foreignProvider.ValidateAsync(UserManager<AccountUser>.ConfirmEmailTokenPurpose, token, manager, user));
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(Guid.NewGuid(), Code(message), Request())).Status);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(user.Id, "invalid+code", Request())).Status);
        var bytes = Encoding.UTF8.GetBytes(token);
        bytes[bytes.Length / 2] ^= 1;
        var altered = WebEncoders.Base64UrlEncode(bytes);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(user.Id, altered, Request())).Status);
        Assert.False(user.EmailConfirmed);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Concurrent_reset_consumption_has_exactly_one_winner()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        Guid userId;
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            userId = (await Register(fixture, setup.ServiceProvider, confirmed: true)).Id;
            await setup.ServiceProvider.GetRequiredService<AccountRecoveryService>().RequestResetAsync(fixture.Email, "/", Request());
        }
        var code = Code(Assert.Single(fixture.Inbox.Messages));
        var passwords = new[] { ReplacementPassword(), ReplacementPassword() };
        var results = await Task.WhenAll(passwords.Select(async (password, index) =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AccountRecoveryService>().ResetPasswordAsync(userId, code, password, Request(index + 1));
        }));
        Assert.Single(results, result => result.Status == AccountOperationStatus.Accepted);
        Assert.Single(results, result => result.Status == AccountOperationStatus.Rejected);
        await using var check = fixture.Services.CreateAsyncScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByIdAsync(userId.ToString()))!;
        Assert.True(await manager.CheckPasswordAsync(user, passwords[0]) ^ await manager.CheckPasswordAsync(user, passwords[1]));
        Assert.Empty(await check.ServiceProvider.GetRequiredService<IdentityStoreContext>().UserTokens.ToListAsync());
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Concurrent_reissue_keeps_only_one_latest_digest_and_credentials_change_invalidates_links()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        Guid userId;
        await using (var setup = fixture.Services.CreateAsyncScope()) { userId = (await Register(fixture, setup.ServiceProvider, confirmed: true)).Id; }
        await Task.WhenAll(Enumerable.Range(1, 2).Select(async index =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AccountRecoveryService>().RequestResetAsync(fixture.Email, "/", Request(index));
        }));
        var messages = fixture.Inbox.Messages.ToArray();
        Assert.Equal(2, messages.Length);
        await using var check = fixture.Services.CreateAsyncScope();
        var database = check.ServiceProvider.GetRequiredService<IdentityStoreContext>();
        var marker = Assert.Single(await database.UserTokens.AsNoTracking().ToListAsync());
        Assert.Single(messages, message => marker.Value == Digest(RecoveryLinks.DecodeCode(Code(message))!));
        Assert.All(messages, message => Assert.True(marker.Value != RecoveryLinks.DecodeCode(Code(message)), "Only token digest may be stored."));
        var latest = messages.Single(message => marker.Value == Digest(RecoveryLinks.DecodeCode(Code(message))!));
        var old = messages.Single(message => !ReferenceEquals(message, latest));
        var recovery = check.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(userId, Code(old), ReplacementPassword(), Request())).Status);
        var manager = check.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByIdAsync(userId.ToString()))!;
        Assert.True((await manager.ChangePasswordAsync(user, fixture.Password, ReplacementPassword())).Succeeded);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(userId, Code(latest), ReplacementPassword(), Request())).Status);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Requests_are_generic_rate_limited_and_cannot_confirm_disabled_or_unverified_reset_accounts()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider);
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        for (var i = 1; i <= 3; i++)
        {
            var known = await recovery.RequestConfirmationAsync(fixture.Email, "/", Request(i));
            var absent = await recovery.RequestConfirmationAsync("absent@example.invalid", "/", Request(i));
            Assert.Equal(known, absent);
            Assert.Equal(AccountOperationStatus.Accepted, known.Status);
            Assert.Null(known.UserId);
        }
        var limited = await recovery.RequestConfirmationAsync(fixture.Email, "/", Request(4));
        Assert.Equal(limited, await recovery.RequestConfirmationAsync("absent@example.invalid", "/", Request(4)));
        Assert.Equal(AccountOperationStatus.Throttled, limited.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), limited.RetryAfter);
        Assert.Equal(3, fixture.Inbox.Messages.Count);
        await recovery.RequestResetAsync(fixture.Email, "/", Request());
        Assert.Equal(3, fixture.Inbox.Messages.Count); // No reset before email verification.
        var message = fixture.Inbox.Messages.ToArray()[2];
        user.IsActive = false;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>().UpdateAsync(user)).Succeeded);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(user.Id, Code(message), Request())).Status);
        Assert.False(user.EmailConfirmed);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Sender_failures_remain_generic_and_logs_never_contain_tokens_links_email_or_passwords()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        var user = await Register(fixture, scope.ServiceProvider, confirmed: true);
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        var correlation = Guid.NewGuid().ToString("N");
        await recovery.RequestResetAsync(fixture.Email, "/workspaces", Request(correlation: correlation));
        var message = Assert.Single(fixture.Inbox.Messages);
        fixture.Inbox.FailDelivery = true;
        var known = await recovery.RequestResetAsync(fixture.Email, "/", Request());
        var absent = await recovery.RequestResetAsync("absent@example.invalid", "/", Request());
        Assert.Equal(known, absent);
        Assert.Equal(AccountOperationStatus.Accepted, known.Status);
        var logs = string.Join('\n', fixture.Logs.Entries);
        Assert.Contains("CorrelationId=" + correlation, logs);
        Assert.Contains("Account recovery delivery failed", logs);
        foreach (var forbidden in new[] { fixture.Email, fixture.Password, user.PasswordHash!, user.SecurityStamp!, message.Link.ToString(), Code(message),
            RecoveryLinks.DecodeCode(Code(message))!, fixture.Connection })
        {
            Assert.True(!logs.Contains(forbidden, StringComparison.OrdinalIgnoreCase), "Sensitive data in captured logs.");
        }
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Recovery_dependency_outages_fail_closed_and_unsafe_redirect_is_rejected_before_delivery()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        await using var scope = fixture.Services.CreateAsyncScope();
        await Register(fixture, scope.ServiceProvider);
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.RequestConfirmationAsync(fixture.Email, "//evil.invalid", Request())).Status);
        Assert.Empty(fixture.Inbox.Messages);
        var offlineDatabase = new Npgsql.NpgsqlConnectionStringBuilder(fixture.Connection) { Port = 1, Timeout = 1 };
        await using (var services = fixture.BuildServices(offlineDatabase.ConnectionString, fixture.Redis, recovery: true))
        await using (var badScope = services.CreateAsyncScope())
        {
            var result = await badScope.ServiceProvider.GetRequiredService<AccountRecoveryService>().RequestConfirmationAsync(fixture.Email, "/", Request());
            Assert.Equal(AccountOperationStatus.Unavailable, result.Status);
        }
        using var offlineRedis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { "127.0.0.1:1" },
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 100,
            AsyncTimeout = 100
        });
        await using (var services = fixture.BuildServices(fixture.Connection, offlineRedis, recovery: true))
        await using (var badScope = services.CreateAsyncScope())
        {
            var result = await badScope.ServiceProvider.GetRequiredService<AccountRecoveryService>().RequestConfirmationAsync(fixture.Email, "/", Request());
            Assert.Equal(AccountOperationStatus.Unavailable, result.Status);
        }
        Assert.Empty(fixture.Inbox.Messages);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Recovery_operation_origin_and_consume_budgets_are_independent_and_expire()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync(recovery: true);
        var limiter = fixture.Services.GetRequiredService<AccountRateLimiter>();
        for (var index = 1; index <= 60; index++)
        {
            Assert.True(await limiter.AllowAsync(AccountRateOperation.RequestEmailConfirmation, $"{index}@EXAMPLE.INVALID", Request(index).Origin, default));
        }
        Assert.False(await limiter.AllowAsync(AccountRateOperation.RequestEmailConfirmation, "overflow@example.invalid", Request(61).Origin, default));
        for (var index = 1; index <= 20; index++)
        {
            Assert.True(await limiter.AllowAsync(AccountRateOperation.RequestPasswordReset, $"{index}@EXAMPLE.INVALID", Request().Origin, default));
        }
        Assert.False(await limiter.AllowAsync(AccountRateOperation.RequestPasswordReset, "overflow@example.invalid", Request().Origin, default));
        await using var scope = fixture.Services.CreateAsyncScope();
        var recovery = scope.ServiceProvider.GetRequiredService<AccountRecoveryService>();
        var userId = Guid.NewGuid();
        for (var index = 1; index <= 5; index++)
        {
            Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ConfirmEmailAsync(userId, "invalid+code", Request(index))).Status);
        }
        Assert.Equal(AccountOperationStatus.Throttled, (await recovery.ConfirmEmailAsync(userId, "invalid+code", Request(6))).Status);
        Assert.Equal(AccountOperationStatus.Rejected, (await recovery.ResetPasswordAsync(userId, "invalid+code", ReplacementPassword(), Request(6))).Status);
        var keys = fixture.Redis.GetServer(fixture.Redis.GetEndPoints()[0]).Keys(pattern: $"ramai:{{{fixture.RateNamespace}}}:confirm-email:account:*").ToArray();
        var accountKey = Assert.Single(keys);
        var ttl = await fixture.Redis.GetDatabase().KeyTimeToLiveAsync(accountKey);
        Assert.InRange(ttl!.Value, TimeSpan.FromMinutes(12), TimeSpan.FromMinutes(15));
    }

    // Test-only aged standard payload, matching Identity's documented/source format;
    // production generation/validation remains entirely in DataProtectorTokenProvider.
    private static async Task<string> SeedStandardTokenAt(AccountSecurityFixture fixture, IServiceProvider services,
        AccountUser user, bool confirmation, DateTimeOffset created)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false, true), leaveOpen: true))
        {
            writer.Write(created.UtcTicks);
            writer.Write(user.Id.ToString());
            writer.Write(confirmation ? UserManager<AccountUser>.ConfirmEmailTokenPurpose : UserManager<AccountUser>.ResetPasswordTokenPurpose);
            writer.Write(user.SecurityStamp!);
        }
        var provider = confirmation ? ConfirmationTokenOptions.ProviderName : ResetTokenOptions.ProviderName;
        var token = Convert.ToBase64String(fixture.Protection.CreateProtector(provider).Protect(buffer.ToArray()));
        var context = services.GetRequiredService<IdentityStoreContext>();
        context.UserTokens.Add(new IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = AccountRecoveryService.MarkerProvider,
            Name = confirmation ? "EmailConfirmation" : "PasswordReset",
            Value = Digest(token)
        });
        await context.SaveChangesAsync();
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }
    private static string Digest(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class InfrastructureTheoryAttribute : TheoryAttribute
{
    public InfrastructureTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("RAMAI_RUN_INFRASTRUCTURE_TESTS") != "1") { Skip = "Requires opt-in PostgreSQL/Redis test infrastructure."; }
    }
}
