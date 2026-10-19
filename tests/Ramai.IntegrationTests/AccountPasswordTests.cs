using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ramai.Modules.Identity.Persistence;
using Ramai.Modules.Identity.Persistence.AccountSecurity;
using Xunit.Abstractions;

namespace Ramai.IntegrationTests;

public sealed class AccountPasswordTests(ITestOutputHelper output)
{
    [Fact]
    public void Embedded_corpus_matches_the_pinned_derived_dataset()
    {
        using var stream = typeof(AccountPasswordValidator).Assembly.GetManifestResourceStream(
            "Ramai.Modules.Identity.Persistence.AccountSecurity.Data.ncsc-eligible.sha256");
        Assert.NotNull(stream);
        Assert.Equal("52732C0E7165FBEC2A1371ED1A9E01B5232D11F84D7E91C06853F0C0A531C8DC",
            Convert.ToHexString(SHA256.HashData(stream)));
    }
    [Theory]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Length_is_measured_as_unicode_scalars(int count, bool valid)
    {
        var password = string.Concat(Enumerable.Repeat("🌊", count));
        Assert.Equal(valid, PasswordText.Normalize(password) is not null);
    }

    [Fact]
    public void Normalization_is_nfc_and_never_trims_truncates_or_changes_case()
    {
        Assert.Equal(" café sailing in space ", PasswordText.Normalize(" cafe\u0301 sailing in space "));
        Assert.NotEqual(PasswordText.Normalize("Fifteen Calm Rivers"), PasswordText.Normalize("fifteen calm rivers"));
        Assert.Null(PasswordText.Normalize("a broken surrogate \uD800"));
        Assert.Null(PasswordText.Normalize(null));
        Assert.Null(PasswordText.Normalize(new string('z', 129)));
    }

    [Theory]
    [InlineData("sailing across amber horizons", true)]
    [InlineData("spaces in a calm passphrase", true)]
    [InlineData("123456789012345", false)]
    [InlineData("all my ramai passwords", false)]
    [InlineData("               ", false)]
    [InlineData("my-accountname-passphrase", false)]
    public async Task Validator_allows_passphrases_and_blocks_common_and_account_related_values(string password, bool accepted)
    {
        var validator = new AccountPasswordValidator();
        var result = await validator.ValidateAsync(null!, new AccountUser { Email = "accountname@example.invalid" }, password);
        Assert.Equal(accepted, result.Succeeded);
        Assert.Equal(329, AccountPasswordValidator.CorpusEntryCount);
    }

    [Fact]
    public void Hasher_is_salted_standard_identity_v3_and_supports_rehash()
    {
        var user = new AccountUser();
        const string password = "cafe\u0301 sailing with amber clouds";
        var hasher = Hasher(AccountSecurityRegistration.HashIterations);
        var first = hasher.HashPassword(user, password);
        var second = hasher.HashPassword(user, password);
        Assert.NotEqual(first, second);
        Assert.NotEqual(password, first);
        var header = Convert.FromBase64String(first);
        Assert.Equal(1, header[0]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(1, 4))); // Identity HMAC-SHA512.
        Assert.Equal(220_000u, BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(5, 4)));
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, first, "café sailing with amber clouds"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, first, "different synthetic passphrase"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, "invalid hash", password));
        var old = Hasher(100_000).HashPassword(user, password);
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, hasher.VerifyHashedPassword(user, old, password));
    }

    [Fact]
    public void Hash_work_factor_has_a_reproducible_local_microbenchmark()
    {
        var user = new AccountUser();
        var hasher = Hasher(AccountSecurityRegistration.HashIterations);
        const string password = "synthetic amber clouds benchmark";
        var hash = hasher.HashPassword(user, password); // Warmup, not a persisted credential.
        var hashTimes = new List<double>();
        var verifyTimes = new List<double>();
        for (var i = 0; i < 7; i++)
        {
            var watch = Stopwatch.StartNew();
            hasher.HashPassword(user, password);
            hashTimes.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(user, hash, password));
            verifyTimes.Add(watch.Elapsed.TotalMilliseconds);
        }
        output.WriteLine("Identity V3 / 220000 iterations, seven samples: median hash {0:F1} ms; median verify {1:F1} ms. Not a capacity or timing-side-channel guarantee.",
            hashTimes.Order().ElementAt(3), verifyTimes.Order().ElementAt(3));
    }

    private static NormalizedPasswordHasher Hasher(int iterations) => new(Options.Create(new PasswordHasherOptions { IterationCount = iterations }));
}
