using System.Net;
using Demizon.Common.Configuration;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CalendarEvent = Google.Apis.Calendar.v3.Data.Event;
using CalendarEventDateTime = Google.Apis.Calendar.v3.Data.EventDateTime;

namespace Demizon.Core.Services.GoogleCalendar;

public sealed class GoogleCalendarService : IGoogleCalendarService, IDisposable
{
    private readonly GoogleCalendarSettings _settings;
    private readonly ILogger<GoogleCalendarService> _logger;
    private readonly GoogleAuthorizationCodeFlow _flow;
    private const string ApplicationName = "Demizon";

    public GoogleCalendarService(
        IOptions<GoogleCalendarSettings> options,
        ILogger<GoogleCalendarService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _settings.ClientId,
                ClientSecret = _settings.ClientSecret
            },
            Scopes = [CalendarService.Scope.Calendar]
        });
    }

    public async Task<string?> ExchangeCodeForRefreshTokenAsync(
        string authorizationCode, CancellationToken ct = default)
    {
        try
        {
            var tokenResponse = await _flow.ExchangeCodeForTokenAsync(
                userId: "user",
                code: authorizationCode,
                redirectUri: _settings.RedirectUri,
                taskCancellationToken: ct);

            return tokenResponse.RefreshToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selhalo získání refresh tokenu výměnou za autorizační kód.");
            return null;
        }
    }

    public async Task<string?> CreateEventAsync(
        string refreshToken,
        string calendarId,
        DateTime dateFrom,
        DateTime? dateTo,
        string? eventTitle,
        CancellationToken ct = default)
    {
        try
        {
            var service = await BuildCalendarServiceAsync(refreshToken, ct);
            var (startTime, endTime) = ResolveTimeRange(dateFrom, dateTo);

            var calEvent = new CalendarEvent
            {
                Summary = eventTitle ?? "Zkouška Demizon",
                Start = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = ToLocalOffset(startTime),
                    TimeZone = _settings.TimeZone
                },
                End = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = ToLocalOffset(endTime),
                    TimeZone = _settings.TimeZone
                },
                Description = "Automaticky přidáno systémem Demizon."
            };

            var request = service.Events.Insert(calEvent, calendarId);
            var createdEvent = await request.ExecuteAsync(ct);
            _logger.LogInformation("Vytvořena Google Calendar událost {EventId} pro datum {Date}.", createdEvent.Id, dateFrom);
            return createdEvent.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selhalo vytvoření Google Calendar události pro datum {Date}.", dateFrom);
            return null;
        }
    }

    public async Task<bool> UpdateEventAsync(
        string refreshToken,
        string calendarId,
        string googleEventId,
        DateTime dateFrom,
        DateTime? dateTo,
        string? eventTitle,
        CancellationToken ct = default)
    {
        try
        {
            var service = await BuildCalendarServiceAsync(refreshToken, ct);
            var (startTime, endTime) = ResolveTimeRange(dateFrom, dateTo);

            var patch = new CalendarEvent
            {
                Summary = eventTitle,
                Start = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = ToLocalOffset(startTime),
                    TimeZone = _settings.TimeZone
                },
                End = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = ToLocalOffset(endTime),
                    TimeZone = _settings.TimeZone
                }
            };

            await service.Events.Patch(patch, calendarId, googleEventId).ExecuteAsync(ct);
            _logger.LogInformation("Aktualizována Google Calendar událost {EventId}.", googleEventId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selhala aktualizace Google Calendar události {EventId}.", googleEventId);
            return false;
        }
    }

    public async Task<bool> DeleteEventAsync(
        string refreshToken,
        string calendarId,
        string googleEventId,
        CancellationToken ct = default)
    {
        try
        {
            var service = await BuildCalendarServiceAsync(refreshToken, ct);
            await service.Events.Delete(calendarId, googleEventId).ExecuteAsync(ct);
            _logger.LogInformation("Smazána Google Calendar událost {EventId}.", googleEventId);
            return true;
        }
        catch (GoogleApiException ex) when
            (ex.HttpStatusCode == HttpStatusCode.NotFound ||
             ex.HttpStatusCode == HttpStatusCode.Gone)
        {
            // Událost již neexistuje — považujeme za úspěch
            _logger.LogWarning("Google Calendar událost {EventId} nebyla nalezena při mazání (pravděpodobně již smazána).", googleEventId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Selhalo smazání Google Calendar události {EventId}.", googleEventId);
            return false;
        }
    }

    /// <summary>
    /// Určí skutečný start/end čas. Pro zkoušky (dateTo == null) použije konfiguraci,
    /// pro akce s dateTo použije reálný rozsah.
    /// </summary>
    private (DateTime start, DateTime end) ResolveTimeRange(DateTime dateFrom, DateTime? dateTo)
    {
        if (dateTo is null)
        {
            // Zkouška — pevný čas z konfigurace
            return (dateFrom.Date.AddHours(_settings.RehearsalStartHour),
                    dateFrom.Date.AddHours(_settings.RehearsalEndHour));
        }

        // Akce s reálným DateFrom/DateTo
        return (dateFrom, dateTo.Value);
    }

    /// <summary>
    /// Převede DateTime na DateTimeOffset v pražské časové zóně se správným offsetem,
    /// včetně letního času (UTC+2 v létě, UTC+1 v zimě).
    /// </summary>
    private DateTimeOffset ToLocalOffset(DateTime localTime)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZone);
        // Ensure Kind is Unspecified so DateTimeOffset constructor doesn't throw for UTC-kind values
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, tz.GetUtcOffset(unspecified));
    }

    private async Task<CalendarService> BuildCalendarServiceAsync(string refreshToken, CancellationToken ct)
    {
        var tokenResponse = new TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(_flow, "user", tokenResponse);

        // Získá čerstvý access token na základě uloženého refresh tokenu
        await credential.RefreshTokenAsync(ct);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }

    public void Dispose() => _flow.Dispose();
}
