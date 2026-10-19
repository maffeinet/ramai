using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Ramai.Modules.Identity.Persistence.AccountRecovery;

public sealed class RecoveryLinks
{
    private readonly Uri _origin;
    public RecoveryLinks(Uri origin)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(origin.Host) ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0)
        {
            throw new ArgumentException("Recovery origin must be a configured HTTPS origin without credentials, path or query.", nameof(origin));
        }
        _origin = origin;
    }

    public static bool IsSafeReturnPath(string? path) => path is { Length: > 0 and <= 256 } &&
        path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal) &&
        path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_' or '.') &&
        !path.Split('/').Any(segment => segment is "." or "..");

    public Uri Create(bool confirmation, Guid userId, string token, string returnPath)
    {
        if (!IsSafeReturnPath(returnPath)) { throw new ArgumentException("Unsafe return path.", nameof(returnPath)); }
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var path = confirmation ? "account/confirm-email" : "account/reset-password";
        return new Uri(_origin, $"{path}?userId={userId:D}&code={code}&returnPath={Uri.EscapeDataString(returnPath)}");
    }

    public static string? DecodeCode(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Length > 8192 || !code.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) { return null; }
        try { return new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (Exception exception) when (exception is FormatException or ArgumentException) { return null; }
    }
}
