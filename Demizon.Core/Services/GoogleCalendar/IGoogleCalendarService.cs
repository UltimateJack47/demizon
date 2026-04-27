namespace Demizon.Core.Services.GoogleCalendar;

public interface IGoogleCalendarService
{
    /// <summary>
    /// Vymění autorizační kód za refresh token (OAuth 2.0 code exchange).
    /// Vrátí refresh token nebo null při selhání.
    /// </summary>
    Task<string?> ExchangeCodeForRefreshTokenAsync(string authorizationCode, CancellationToken ct = default);

    /// <summary>
    /// Vytvoří událost v Google Calendar pro daný datum a název.
    /// Pro páteční zkoušky (TimeOfDay == Zero) použije pevný čas z konfigurace.
    /// Pro akce (DateFrom s reálným časem) použije skutečný čas; dateTo určuje konec.
    /// Vrátí Google Event ID nebo null při selhání.
    /// </summary>
    Task<string?> CreateEventAsync(string refreshToken, string calendarId, DateTime dateFrom, DateTime? dateTo, string? eventTitle, CancellationToken ct = default);

    /// <summary>
    /// Aktualizuje existující událost v Google Calendar (čas, název).
    /// Vrátí true při úspěchu, false při selhání.
    /// </summary>
    Task<bool> UpdateEventAsync(string refreshToken, string calendarId, string googleEventId, DateTime dateFrom, DateTime? dateTo, string? eventTitle, CancellationToken ct = default);

    /// <summary>
    /// Smaže událost z Google Calendar dle ID.
    /// 404/410 se považuje za úspěch (idempotentní smazání).
    /// </summary>
    Task<bool> DeleteEventAsync(string refreshToken, string calendarId, string googleEventId, CancellationToken ct = default);
}
