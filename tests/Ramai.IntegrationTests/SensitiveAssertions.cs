namespace Ramai.IntegrationTests;

// A failed leak check must not itself publish the secret, response or captured log.
internal static class SensitiveAssertions
{
    public static void Absent(string payload, params string[] canaries)
    {
        foreach (var canary in canaries)
        {
            Assert.False(string.IsNullOrEmpty(canary), "Sensitive canary was not populated; values withheld.");
            Assert.False(payload.Contains(canary, StringComparison.Ordinal), "Sensitive material detected; values and payload withheld.");
        }
    }
}

public sealed class SensitiveAssertionTests
{
    [Fact]
    public void Clean_payload_is_accepted() => SensitiveAssertions.Absent("clean response", "synthetic-canary");

    [Fact]
    public void Detection_fails_without_including_canary_or_payload_in_diagnostics()
    {
        const string canary = "synthetic-canary";
        var failure = Record.Exception(() => SensitiveAssertions.Absent("private payload " + canary, canary));
        Assert.NotNull(failure);
        Assert.DoesNotContain(canary, failure.ToString());
        Assert.DoesNotContain("private payload", failure.ToString());
    }

    [Fact]
    public void Empty_canary_is_a_failure_not_a_vacuous_success() =>
        Assert.NotNull(Record.Exception(() => SensitiveAssertions.Absent("clean response", "")));
}
