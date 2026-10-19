using Microsoft.AspNetCore.WebUtilities;
using Ramai.Modules.Identity.Persistence.AccountRecovery;

namespace Ramai.IntegrationTests;

public sealed class RecoveryLinksTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/workspaces", true)]
    [InlineData("/account/profile", true)]
    [InlineData("//evil.invalid", false)]
    [InlineData("/\\evil.invalid", false)]
    [InlineData("/%2f%2fevil.invalid", false)]
    [InlineData("/path?return=https://evil.invalid", false)]
    [InlineData("/../account", false)]
    [InlineData("https://evil.invalid", false)]
    [InlineData("/account#fragment", false)]
    [InlineData("/path\r\nLocation: evil", false)]
    [InlineData("", false)]
    public void Return_paths_are_strictly_local_and_canonical(string path, bool safe) =>
        Assert.Equal(safe, RecoveryLinks.IsSafeReturnPath(path));

    [Theory]
    [InlineData("http://ramai.example.invalid")]
    [InlineData("https://ramai.example.invalid/path")]
    [InlineData("https://ramai.example.invalid?host=evil")]
    [InlineData("https://ramai.example.invalid/#fragment")]
    public void Origin_is_server_configured_https_only(string origin) =>
        Assert.Throws<ArgumentException>(() => new RecoveryLinks(new Uri(origin)));

    [Fact]
    public void Messages_and_links_do_not_expose_email_or_credentials_through_ToString()
    {
        var links = new RecoveryLinks(new Uri("https://ramai.example.invalid"));
        var link = links.Create(true, Guid.NewGuid(), "synthetic protected token", "/workspaces");
        var message = new AccountRecoveryMessage("synthetic@example.invalid", RecoveryPurpose.EmailConfirmation, link);
        Assert.Equal(nameof(AccountRecoveryMessage), message.ToString());
        Assert.False(link.Query.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("/workspaces", QueryHelpers.ParseQuery(link.Query)["returnPath"].ToString());
        Assert.True(RecoveryLinks.DecodeCode(QueryHelpers.ParseQuery(link.Query)["code"]) == "synthetic protected token");
        Assert.Null(RecoveryLinks.DecodeCode("invalid+code"));
        Assert.Null(RecoveryLinks.DecodeCode(new string('x', 8193)));
        Assert.Throws<ArgumentException>(() => links.Create(false, Guid.NewGuid(), "synthetic", "//evil.invalid"));
    }
}
