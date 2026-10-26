namespace Demizon.Mvc.Services.Costume;

public enum CostumeScanKind
{
    Item,
    Member
}

/// <summary>Co bylo naskenováno nebo napsáno: kód kusu, nebo QR token člena.</summary>
public sealed record CostumeScan(CostumeScanKind Kind, string Value)
{
    /// <summary>
    /// Bere jen poslední segment URL, takže štítky přežijí změnu domény (<c>…/k/K0042</c> i
    /// <c>https://jina-domena/k/K0042</c>). Text bez URL je ručně napsaný kód kusu — nebo text, který
    /// do pole napsala USB/bluetooth čtečka. Null pro prázdný vstup.
    /// </summary>
    public static CostumeScan? Parse(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var path = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsolutePath
            : trimmed;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        var last = Uri.UnescapeDataString(segments[^1]);
        var marker = segments.Length >= 2 ? segments[^2] : null;
        return string.Equals(marker, CostumeQrCodes.MemberPath, StringComparison.OrdinalIgnoreCase)
            ? new CostumeScan(CostumeScanKind.Member, last)
            : new CostumeScan(CostumeScanKind.Item, last);
    }
}
