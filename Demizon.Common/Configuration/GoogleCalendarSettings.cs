using System.ComponentModel.DataAnnotations;

namespace Demizon.Common.Configuration;

public class GoogleCalendarSettings
{
    /// <summary>
    /// OAuth Client ID z Google Cloud Console. Pokud není nastaveno, integrace s Google Calendar je zakázána.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth Client Secret z Google Cloud Console. Pokud není nastaveno, integrace s Google Calendar je zakázána.
    /// </summary>
    public string? ClientSecret { get; set; }

    [Required]
    public string RedirectUri { get; set; } = null!;

    /// <summary>
    /// Vrátí true pokud jsou OAuth credentials nakonfigurované a integrace může fungovat.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>
    /// ID Google Calendar, do kterého se zapisují události. Výchozí "primary" = hlavní kalendář uživatele.
    /// </summary>
    public string DefaultCalendarId { get; set; } = "primary";

    /// <summary>
    /// IANA identifikátor časové zóny pro vytváření událostí.
    /// </summary>
    public string TimeZone { get; set; } = "Europe/Prague";

    /// <summary>
    /// Hodina začátku páteční zkoušky (24h formát).
    /// </summary>
    public int RehearsalStartHour { get; set; } = 19;

    /// <summary>
    /// Hodina konce páteční zkoušky (24h formát).
    /// </summary>
    public int RehearsalEndHour { get; set; } = 21;

}
