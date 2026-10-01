using Demizon.Common;
using Demizon.Common.Configuration;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Demizon.Core.Services.Rehearsal;

public sealed class RehearsalService(
    DemizonContext db,
    IOptions<RehearsalSettings> rehearsalOptions,
    IOptions<GoogleCalendarSettings> calendarOptions,
    ILogger<RehearsalService> logger) : IRehearsalService
{
    /// <summary>Pojistka proti překlepu v roce: výjimka na deset let by tiše zrušila všechno.</summary>
    public const int MaxCancellationDays = 366;

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

        var cancellations = await GetCancellationsAsync(from, toExclusive, ct);

        return RehearsalSchedule.Compute(
            from, toExclusive, _rule,
            events.Select(e => new EventSpan(e.DateFrom, e.DateTo, IsCancelled: false)),
            cancellations.Select(c => new CancellationSpan(c.Id, c.DateFrom, c.DateTo, c.Reason)));
    }

    public async Task<RehearsalOccurrence?> GetAsync(DateOnly date, CancellationToken ct = default)
    {
        var occurrences = await GetRangeAsync(date, date.AddDays(1), ct);
        return occurrences.Count == 0 ? null : occurrences[0];
    }

    public async Task<IReadOnlyList<RehearsalCancellation>> GetCancellationsAsync(
        DateOnly from, DateOnly toExclusive, CancellationToken ct = default)
    {
        return await db.RehearsalCancellations
            .AsNoTracking()
            .Include(c => c.CreatedBy)
            .Where(c => c.DateFrom < toExclusive && c.DateTo >= from)
            .OrderBy(c => c.DateFrom)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<RehearsalCancellation?> GetCancellationAsync(int id, CancellationToken ct = default)
    {
        return await db.RehearsalCancellations
            .AsNoTracking()
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Result<int>> CancelAsync(
        DateOnly from, DateOnly to, string? reason, int? byMemberId, CancellationToken ct = default)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (to < from)
            return Result<int>.Rejected("Konec období je před začátkem.");
        if (to.DayNumber - from.DayNumber + 1 > MaxCancellationDays)
            return Result<int>.Rejected($"Najednou jde zrušit nejvýš {MaxCancellationDays} dní.");
        if (reason?.Length > DemizonContext.RehearsalCancellationReasonMaxLength)
            return Result<int>.Rejected(
                $"Důvod může mít nejvýš {DemizonContext.RehearsalCancellationReasonMaxLength} znaků.");

        try
        {
            // Výjimka bez jediné zkoušky by v seznamu jen mátla (úterý, pátek s akcí).
            var occurrences = await GetRangeAsync(from, to.AddDays(1), ct);
            if (occurrences.Count == 0)
                return Result<int>.Rejected("V tomto období žádná zkouška není.");
            if (occurrences.All(o => o.IsCancelled))
                return Result<int>.Rejected("Zkoušky v tomto období už jsou zrušené.");

            var cancellation = new RehearsalCancellation
            {
                DateFrom = from,
                DateTo = to,
                Reason = reason,
                CreatedByMemberId = byMemberId,
                CreatedAt = DateTime.UtcNow,
            };
            db.RehearsalCancellations.Add(cancellation);
            await db.SaveChangesAsync(ct);
            return Result<int>.Ok(cancellation.Id);
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to cancel rehearsals {From}–{To}.", from, to);
            return Result<int>.Fail("Zkoušky se nepodařilo zrušit.");
        }
    }

    public async Task<Result> RestoreAsync(int cancellationId, CancellationToken ct = default)
    {
        try
        {
            var cancellation = await db.RehearsalCancellations.FindAsync([cancellationId], ct);
            if (cancellation is null)
                return Result.NotFound($"Výjimka s id {cancellationId} nebyla nalezena.");

            db.RehearsalCancellations.Remove(cancellation);
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Failed to restore rehearsal cancellation {Id}.", cancellationId);
            return Result.Fail("Výjimku se nepodařilo zrušit.");
        }
    }
}
