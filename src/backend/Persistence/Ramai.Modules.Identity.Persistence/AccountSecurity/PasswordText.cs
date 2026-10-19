using System.Text;

namespace Ramai.Modules.Identity.Persistence.AccountSecurity;

public static class PasswordText
{
    // NFC, Unicode scalar values (not UTF-16 code units or grapheme clusters).
    // No trim, case folding, composition requirements or silent truncation.
    public static string? Normalize(string? password)
    {
        if (password is null)
        {
            return null;
        }
        try
        {
            var normalized = password.Normalize(NormalizationForm.FormC);
            return normalized.EnumerateRunes().Count() is >= 15 and <= 128 ? normalized : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
