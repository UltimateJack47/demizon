using Demizon.Common.Exceptions;
using Demizon.Core.Services.GoogleCalendar;

namespace Demizon.Tests.Unit.Fakes;

/// <summary>
/// Dvojník nad <see cref="IGoogleCalendarService"/>. Skutečná implementace
/// obaluje Google API, takže se v testu použít nedá — a přitom to není ona,
/// co je potřeba otestovat: zajímavá je <b>kompenzační logika volajících</b>,
/// tedy co se stane, když se v kalendáři něco povede a uložení do databáze pak ne.
/// </summary>
/// <remarks>
/// Rozhraní v repu existovalo už dřív; chyběl jen dvojník a testy nad ním
/// (<c>testing-plan.md</c> to vedl jako „chtěl by rozhraní“, což už neplatilo).
/// </remarks>
public sealed class FakeGoogleCalendarService(CalendarTestState state) : IGoogleCalendarService
{
    public Task<string?> ExchangeCodeForRefreshTokenAsync(
        string authorizationCode, CancellationToken ct = default) =>
        Task.FromResult<string?>("fake-refresh-token");

    public Task<string?> CreateEventAsync(
        string refreshToken, string calendarId, DateTime dateFrom, DateTime? dateTo,
        string? eventTitle, CancellationToken ct = default)
    {
        if (state.ThrowTokenRevoked)
            throw new GoogleTokenRevokedException();

        var id = state.CreatedEventId;
        if (id is not null)
            state.RecordCreated(id);

        return Task.FromResult(id);
    }

    public Task<bool> UpdateEventAsync(
        string refreshToken, string calendarId, string googleEventId, DateTime dateFrom,
        DateTime? dateTo, string? eventTitle, CancellationToken ct = default)
    {
        if (state.ThrowTokenRevoked)
            throw new GoogleTokenRevokedException();

        return Task.FromResult(true);
    }

    public Task<bool> DeleteEventAsync(
        string refreshToken, string calendarId, string googleEventId, CancellationToken ct = default)
    {
        if (state.ThrowTokenRevoked)
            throw new GoogleTokenRevokedException();

        state.RecordDeleted(googleEventId);
        return Task.FromResult(true);
    }
}
