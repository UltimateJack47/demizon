using System.ComponentModel.DataAnnotations;

namespace Demizon.Common.Configuration;

public class GoogleCalendarSettings
{
    [Required]
    public string ClientId { get; set; } = null!;

    [Required]
    public string ClientSecret { get; set; } = null!;

    [Required]
    public string RedirectUri { get; set; } = null!;

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

    /// <summary>
    /// Délka události s reálným DateFrom (akce) v hodinách.
    /// </summary>
    public int EventDurationHours { get; set; } = 2;
}
