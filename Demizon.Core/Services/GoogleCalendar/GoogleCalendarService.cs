using Demizon.Common.Configuration;
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

public sealed class GoogleCalendarService(
    IOptions<GoogleCalendarSettings> options,
    ILogger<GoogleCalendarService> logger)
    : IGoogleCalendarService
{
    private readonly GoogleCalendarSettings _settings = options.Value;
    private const string ApplicationName = "Demizon";

    public async Task<string?> ExchangeCodeForRefreshTokenAsync(
        string authorizationCode, CancellationToken ct = default)
    {
        try
        {
            var flow = BuildFlow();
            var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                userId: "user",
                code: authorizationCode,
                redirectUri: _settings.RedirectUri,
                taskCancellationToken: ct);

            return tokenResponse.RefreshToken;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selhalo získání refresh tokenu výměnou za autorizační kód.");
            return null;
        }
    }

    public async Task<string?> CreateEventAsync(
        string refreshToken,
        string calendarId,
        DateTime date,
        string? eventTitle,
        CancellationToken ct = default)
    {
        try
        {
            var service = await BuildCalendarServiceAsync(refreshToken, ct);

            DateTime startTime;
            DateTime endTime;

            if (date.TimeOfDay == TimeSpan.Zero)
            {
                // Páteční zkouška bez akce — pevný čas z konfigurace
                startTime = date.Date.AddHours(_settings.RehearsalStartHour);
                endTime = date.Date.AddHours(_settings.RehearsalEndHour);
            }
            else
            {
                // Akce s reálným DateFrom — použijeme skutečný čas
                startTime = date;
                endTime = date.AddHours(2);
            }

            var calEvent = new CalendarEvent
            {
                Summary = eventTitle ?? "Zkouška Demizon",
                Start = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(startTime, TimeSpan.FromHours(1)),
                    TimeZone = _settings.TimeZone
                },
                End = new CalendarEventDateTime
                {
                    DateTimeDateTimeOffset = new DateTimeOffset(endTime, TimeSpan.FromHours(1)),
                    TimeZone = _settings.TimeZone
                },
                Description = "Automaticky přidáno systémem Demizon."
            };

            var request = service.Events.Insert(calEvent, calendarId);
            var createdEvent = await request.ExecuteAsync(ct);
            logger.LogInformation("Vytvořena Google Calendar událost {EventId} pro datum {Date}.", createdEvent.Id, date);
            return createdEvent.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selhalo vytvoření Google Calendar události pro datum {Date}.", date);
            return null;
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
            logger.LogInformation("Smazána Google Calendar událost {EventId}.", googleEventId);
            return true;
        }
        catch (Google.GoogleApiException ex) when
            (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound ||
             ex.HttpStatusCode == System.Net.HttpStatusCode.Gone)
        {
            // Událost již neexistuje — považujeme za úspěch
            logger.LogWarning("Google Calendar událost {EventId} nebyla nalezena při mazání (pravděpodobně již smazána).", googleEventId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selhalo smazání Google Calendar události {EventId}.", googleEventId);
            return false;
        }
    }

    private GoogleAuthorizationCodeFlow BuildFlow() => new(
        new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets
            {
                ClientId = _settings.ClientId,
                ClientSecret = _settings.ClientSecret
            },
            Scopes = [CalendarService.Scope.Calendar]
        });

    private async Task<CalendarService> BuildCalendarServiceAsync(string refreshToken, CancellationToken ct)
    {
        var flow = BuildFlow();
        var tokenResponse = new TokenResponse { RefreshToken = refreshToken };
        var credential = new UserCredential(flow, "user", tokenResponse);

        // Získá čerstvý access token na základě uloženého refresh tokenu
        await credential.RefreshTokenAsync(ct);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }
}
