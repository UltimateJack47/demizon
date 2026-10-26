using System.Text.RegularExpressions;
using Demizon.Dal;

namespace Demizon.Core.Services.Costume;

/// <summary>
/// Inventární kódy. Kód je poslední segment URL v QR (<c>…/k/K0042</c>), proto jen písmena,
/// číslice a pomlčka — nic, co by se v URL muselo escapovat nebo se na štítku pletlo.
/// </summary>
public static partial class CostumeCodes
{
    public const string Prefix = "K";

    [GeneratedRegex("^[A-Z0-9-]+$")]
    private static partial Regex Allowed();

    [GeneratedRegex(@"^K(\d+)$")]
    private static partial Regex Generated();

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static string? Validate(string normalized) =>
        normalized.Length == 0 ? "Kód nesmí být prázdný."
        : normalized.Length > DemizonContext.CostumeCodeMaxLength
            ? $"Kód může mít nejvýš {DemizonContext.CostumeCodeMaxLength} znaků."
        : !Allowed().IsMatch(normalized) ? "Kód smí obsahovat jen písmena bez diakritiky, číslice a pomlčku."
        : null;

    /// <summary>Další volný kód řady <c>K0001</c> podle nejvyššího existujícího.</summary>
    public static string Next(IEnumerable<string> existingCodes)
    {
        var max = existingCodes
            .Select(c => Generated().Match(c))
            .Where(m => m.Success && int.TryParse(m.Groups[1].Value, out _))
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return $"{Prefix}{max + 1:D4}";
    }
}
