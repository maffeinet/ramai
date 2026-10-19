using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Ramai.IntegrationTests;

public sealed class AccountSecurityTests(ITestOutputHelper output)
{
    private static AccountRequestContext Request(string? correlationId = null, int origin = 1) =>
        new(IPAddress.Parse($"192.0.2.{origin}"), correlationId ?? Guid.NewGuid().ToString("N"));

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Registration_is_generic_uses_one_store_and_never_confirms_email_or_issues_session()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        var first = await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        var duplicate = await service.RegisterAsync(fixture.Email.ToUpperInvariant(), fixture.Password, Request());
        Assert.Equal(first, duplicate);
        Assert.Equal(AccountOperationStatus.Accepted, first.Status);
        Assert.Null(first.UserId);
        var database = scope.ServiceProvider.GetRequiredService<IdentityStoreContext>();
        var user = Assert.Single(await database.Users.ToListAsync());
        Assert.False(user.EmailConfirmed);
        Assert.True(user.IsActive);
        Assert.True(user.LockoutEnabled);
        Assert.NotEqual(fixture.Password, user.PasswordHash);
        Assert.False(string.IsNullOrEmpty(user.SecurityStamp));
        Assert.Equal(AccountOperationStatus.Rejected, (await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request())).Status);
        Assert.False(user.EmailConfirmed);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
        Assert.Empty(options.Tokens.ProviderMap);
        Assert.Null(scope.ServiceProvider.GetService<SignInManager<AccountUser>>());
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Fifth_failure_locks_temporarily_and_expiry_or_success_restores_normal_counter_progression()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true; // Synthetic fixture only, not an email flow.
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(AccountOperationStatus.Rejected, (await service.CheckCredentialsAsync(fixture.Email, "wrong", Request(origin: i))).Status);
            Assert.Equal(i, user.AccessFailedCount);
            Assert.False(await manager.IsLockedOutAsync(user));
        }
        var before = DateTimeOffset.UtcNow;
        Assert.Equal(AccountOperationStatus.Rejected, (await service.CheckCredentialsAsync(fixture.Email, "wrong", Request(origin: 5))).Status);
        Assert.True(await manager.IsLockedOutAsync(user));
        Assert.InRange(user.LockoutEnd!.Value, before.AddMinutes(15), DateTimeOffset.UtcNow.AddMinutes(15));
        Assert.Equal(AccountOperationStatus.Rejected, (await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request())).Status);
        Assert.True((await manager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        Assert.Equal(AccountOperationStatus.Rejected, (await service.CheckCredentialsAsync(fixture.Email, "wrong", Request())).Status);
        Assert.Equal(1, user.AccessFailedCount);
        var valid = await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request());
        Assert.Equal(AccountOperationStatus.Accepted, valid.Status);
        Assert.Equal(user.Id, valid.UserId);
        Assert.Equal(0, user.AccessFailedCount);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Concurrent_scopes_cannot_lose_failure_counts_or_reveal_absent_locked_disabled_accounts()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using (var setup = fixture.Services.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<AccountCredentialService>().RegisterAsync(fixture.Email, fixture.Password, Request());
        }
        var failures = await Task.WhenAll(Enumerable.Range(1, 5).Select(async origin =>
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AccountCredentialService>().CheckCredentialsAsync(fixture.Email,
                "wrong but long enough", Request(origin: origin));
        }));
        Assert.All(failures, result => Assert.Equal(new AccountOperationResult(AccountOperationStatus.Rejected), result));
        await using var check = fixture.Services.CreateAsyncScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        Assert.True(await manager.IsLockedOutAsync(user));
        var service = check.ServiceProvider.GetRequiredService<AccountCredentialService>();
        var locked = await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request());
        var absent = await service.CheckCredentialsAsync("absent@example.invalid", fixture.Password, Request());
        Assert.Equal(locked, absent);
        user.IsActive = false;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        Assert.Equal(absent, await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request()));
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Successful_check_rehashes_older_identity_hash_without_logging_credentials_or_account_details()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true;
        var oldHash = new PasswordHasher<AccountUser>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }))
            .HashPassword(user, fixture.Password);
        user.PasswordHash = oldHash;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        var correlation = Guid.NewGuid().ToString("N");
        Assert.Equal(AccountOperationStatus.Accepted, (await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request(correlation))).Status);
        Assert.NotEqual(oldHash, user.PasswordHash);
        await service.CheckCredentialsAsync("absent@example.invalid", "wrong", Request());
        var logs = string.Join('\n', fixture.Logs.Entries);
        Assert.Contains("CorrelationId=" + correlation, logs);
        Assert.Contains("Account security operation", logs);
        foreach (var forbidden in new[] { fixture.Password, oldHash, user.PasswordHash!, fixture.Email, "192.0.2.1", fixture.Connection })
        {
            Assert.DoesNotContain(forbidden, logs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Redis_limits_account_across_origins_origin_across_accounts_and_global_burst_with_ttl()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var limiter = fixture.Services.GetRequiredService<AccountRateLimiter>();
        for (var i = 1; i <= 10; i++)
        {
            Assert.True(await limiter.AllowAsync(false, "SAME@EXAMPLE.INVALID", Request(origin: i).Origin, default));
        }
        Assert.False(await limiter.AllowAsync(false, "SAME@EXAMPLE.INVALID", Request(origin: 11).Origin, default));
        for (var i = 1; i <= 120; i++)
        {
            Assert.True(await limiter.AllowAsync(true, $"{i}@EXAMPLE.INVALID", Request().Origin, default));
        }
        Assert.False(await limiter.AllowAsync(true, "extra@example.invalid", Request().Origin, default));
        // Unique origin and account still cannot bypass the distributed global cap.
        var globalNamespace = "global-" + Guid.NewGuid().ToString("N");
        using var global = new AccountRateLimiter(fixture.Redis, fixture.RateKey, globalNamespace);
        for (var i = 0; i < 300; i++)
        {
            Assert.True(await global.AllowAsync(false, $"{i}@EXAMPLE.INVALID", IPAddress.Parse($"198.51.{i / 250}.{i % 250 + 1}"), default));
        }
        Assert.False(await global.AllowAsync(false, "overflow@example.invalid", IPAddress.Parse("203.0.113.1"), default));
        var redis = fixture.Redis.GetDatabase();
        var keys = fixture.Redis.GetServer(fixture.Redis.GetEndPoints()[0]).Keys(pattern: $"ramai:{{{fixture.RateNamespace}}}:*").ToArray();
        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.DoesNotContain("EXAMPLE", key.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("192.0.2", key.ToString());
            var ttl = await redis.KeyTimeToLiveAsync(key);
            Assert.InRange(ttl!.Value, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            await redis.KeyExpireAsync(key, TimeSpan.FromMilliseconds(1));
        }
        await Task.Delay(25);
        Assert.True(await limiter.AllowAsync(false, "SAME@EXAMPLE.INVALID", Request().Origin, default));
        Assert.Equal(TimeSpan.FromMinutes(1), new AccountOperationResult(AccountOperationStatus.Throttled).RetryAfter);
        // Explicitly remove only this additional test namespace's keys.
        var globalKeys = fixture.Redis.GetServer(fixture.Redis.GetEndPoints()[0]).Keys(pattern: $"ramai:{{{globalNamespace}}}:*").ToArray();
        if (globalKeys.Length > 0) { await redis.KeyDeleteAsync(globalKeys); }
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Dependency_outages_fail_closed_without_leaking_connection_or_exception_details()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        var badDatabase = new Npgsql.NpgsqlConnectionStringBuilder(fixture.Connection) { Port = 1, Timeout = 1 };
        await using (var services = fixture.BuildServices(badDatabase.ConnectionString, fixture.Redis))
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AccountCredentialService>()
                .RegisterAsync(fixture.Email, fixture.Password, Request());
            Assert.Equal(new AccountOperationResult(AccountOperationStatus.Unavailable), result);
        }
        using var offline = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { "127.0.0.1:1" },
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 100,
            AsyncTimeout = 100
        });
        await using (var services = fixture.BuildServices(fixture.Connection, offline))
        await using (var scope = services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<AccountCredentialService>()
                .CheckCredentialsAsync(fixture.Email, fixture.Password, Request());
            Assert.Equal(new AccountOperationResult(AccountOperationStatus.Unavailable), result);
        }
        var logs = string.Join('\n', fixture.Logs.Entries);
        Assert.DoesNotContain(fixture.Connection, logs);
        Assert.DoesNotContain(fixture.Password, logs);
        Assert.DoesNotContain(fixture.Email, logs);
    }

    [Fact]
    public void Rate_key_and_request_context_require_valid_server_inputs()
    {
        Assert.Throws<ArgumentException>(() => new AccountRequestContext(IPAddress.Loopback, "invalid\r\nvalue"));
        Assert.Throws<ArgumentException>(() => new AccountRateLimiter(null!, RandomNumberGenerator.GetBytes(16), "test"));
        Assert.Throws<ArgumentException>(() => new AccountRateLimiter(null!, RandomNumberGenerator.GetBytes(32), "unsafe:namespace"));
        Assert.Equal(IPAddress.Loopback, new AccountRequestContext(IPAddress.Parse("::ffff:127.0.0.1"), "valid-id").Origin);
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Throttling_is_shared_across_instances_and_does_not_reveal_account_presence()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        for (var i = 1; i <= 10; i++)
        {
            Assert.Equal(AccountOperationStatus.Rejected,
                (await service.CheckCredentialsAsync(fixture.Email, "wrong", Request(origin: i))).Status);
            Assert.Equal(AccountOperationStatus.Rejected,
                (await service.CheckCredentialsAsync("absent@example.invalid", "wrong", Request(origin: i))).Status);
        }
        var existing = await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request(origin: 11));
        var absent = await service.CheckCredentialsAsync("absent@example.invalid", fixture.Password, Request(origin: 11));
        Assert.Equal(existing, absent);
        Assert.Equal(AccountOperationStatus.Throttled, existing.Status);
        Assert.Equal(TimeSpan.FromMinutes(1), existing.RetryAfter);
        using var otherInstance = new AccountRateLimiter(fixture.Redis, fixture.RateKey, fixture.RateNamespace);
        Assert.False(await otherInstance.AllowAsync(false, fixture.Email.ToUpperInvariant(), Request(origin: 12).Origin, default));
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task Absent_and_present_failures_do_comparable_password_work_without_claiming_constant_time()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        var present = new List<double>();
        var absent = new List<double>();
        for (var i = 1; i <= 3; i++)
        {
            var watch = Stopwatch.StartNew();
            var known = await service.CheckCredentialsAsync(fixture.Email, "wrong but long enough", Request(origin: i));
            present.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            var missing = await service.CheckCredentialsAsync("absent@example.invalid", "wrong but long enough", Request(origin: i));
            absent.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(known, missing);
        }
        output.WriteLine("Local synthetic sample (three each), median failure: present {0:F1} ms; absent {1:F1} ms. DB work differs; no constant-time or enumeration-resistance proof.",
            present.Order().ElementAt(1), absent.Order().ElementAt(1));
    }

    [InfrastructureFact]
    [Trait("Category", "Infrastructure")]
    public async Task An_account_with_lockout_disabled_cannot_bypass_required_protection()
    {
        await using var fixture = await AccountSecurityFixture.CreateAsync();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountCredentialService>();
        await service.RegisterAsync(fixture.Email, fixture.Password, Request());
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountUser>>();
        var user = (await manager.FindByEmailAsync(fixture.Email))!;
        user.EmailConfirmed = true;
        user.LockoutEnabled = false;
        Assert.True((await manager.UpdateAsync(user)).Succeeded);
        Assert.Equal(new AccountOperationResult(AccountOperationStatus.Rejected),
            await service.CheckCredentialsAsync(fixture.Email, fixture.Password, Request()));
    }
}
