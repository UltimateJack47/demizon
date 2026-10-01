using Demizon.Common.Configuration;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.Rehearsal;

public sealed class RehearsalService(
    DemizonContext db,
    IOptions<RehearsalSettings> rehearsalOptions,
    IOptions<GoogleCalendarSettings> calendarOptions) : IRehearsalService
{
    private readonly RehearsalRule _rule =
        RehearsalRule.From(rehearsalOptions.Value, calendarOptions.Value.TimeZone);

    public async Task<IReadOnlyList<RehearsalOccurrence>> GetRangeAsync(
        DateOnly from, DateOnly toExclusive, CancellationToken ct = default)
    {
        if (toExclusive <= from)
            return [];

        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = toExclusive.ToDateTime(TimeOnly.MinValue);

        // Akce, které do rozsahu zasahují, ne jen ty, které v něm začínají:
        // vícedenní akce od čtvrtka minulého měsíce může schovat první pátek.
        // Druhá podmínka chytí akci s koncem před začátkem.
        var events = await db.Events
            .AsNoTracking()
            .Where(e => !e.IsCancelled
                        && e.DateFrom < rangeEnd
                        && (e.DateTo >= rangeStart || e.DateFrom >= rangeStart))
            .Select(e => new { e.DateFrom, e.DateTo })
            .ToListAsync(ct);

        return RehearsalSchedule.Compute(
            from, toExclusive, _rule,
            events.Select(e => new EventSpan(e.DateFrom, e.DateTo, IsCancelled: false)),
            []);
    }

    public async Task<RehearsalOccurrence?> GetAsync(DateOnly date, CancellationToken ct = default)
    {
        var occurrences = await GetRangeAsync(date, date.AddDays(1), ct);
        return occurrences.Count == 0 ? null : occurrences[0];
    }
}
