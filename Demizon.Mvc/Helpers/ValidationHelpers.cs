using System.Text.RegularExpressions;

namespace Demizon.Mvc.Helpers;

public static partial class ValidationHelpers
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex EmailRegexGenerated();

    public static string? ValidateEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        return EmailRegexGenerated().IsMatch(email) ? null : "Neplatný formát e-mailu";
    }
}
