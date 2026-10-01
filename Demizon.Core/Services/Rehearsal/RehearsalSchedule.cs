using Demizon.Common.Configuration;

namespace Demizon.Core.Services.Rehearsal;

public enum RehearsalState
{
    Scheduled,
    Cancelled,
}

/// <summary>
/// Jeden výskyt pravidelné zkoušky. Virtuální: v databázi existuje jen jako
/// docházka (<c>Attendance { EventId = null, Date = <see cref="Day"/> }</c>),
/// a to až když ji někdo vyplní.
/// </summary>
/// <param name="StartsAt">Místní čas (<see cref="DateTimeKind.Unspecified"/>), stejně jako <c>Event.DateFrom</c>.</param>
/// <param name="EndsAt">Místní čas, viz <paramref name="StartsAt"/>.</param>
public sealed record RehearsalOccurrence(
    DateOnly Date,
    DateTime StartsAt,
    DateTime EndsAt,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    RehearsalState State,
    int? CancellationId,
    string? CancellationReason)
{
    public bool IsCancelled => State == RehearsalState.Cancelled;

    /// <summary>Půlnoc dne zkoušky, klíč docházky v <c>Attendance.Date</c>.</summary>
    public DateTime Day => Date.ToDateTime(TimeOnly.MinValue);
}

public sealed record RehearsalRule(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan Duration,
    TimeZoneInfo TimeZone)
{
    public static RehearsalRule From(RehearsalSettings settings, string timeZoneId) =>
        new(settings.DayOfWeek, settings.StartTime, settings.Duration,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
}

/// <summary>
/// Akce tak, jak leží v databázi. Časy jsou <b>místní</b>, ne UTC: klienti je posílají
/// bez offsetu a nic je nepřevádí (Flutter ISO bez <c>Z</c>, Blazor date picker půlnoc).
/// </summary>
public readonly record struct EventSpan(DateTime DateFrom, DateTime DateTo, bool IsCancelled);

/// <param name="DateTo">Včetně. Jedna zkouška má <c>DateFrom == DateTo</c>.</param>
public readonly record struct CancellationSpan(int Id, DateOnly DateFrom, DateOnly DateTo, string? Reason);

/// <summary>
/// Jediné místo, které ví, kdy je zkouška. Bez přístupu k databázi, data dodává volající.
/// </summary>
public static class RehearsalSchedule
{
    /// <summary>
    /// Výskyty v rozsahu <c>[from, toExclusive)</c>. Den, přes který vede nezrušená
    /// akce, zkoušku nemá. Zrušená zkouška se vrací se stavem
    /// <see cref="RehearsalState.Cancelled"/>, aby ji členové viděli jako „odpadá“.
    /// </summary>
    public static IReadOnlyList<RehearsalOccurrence> Compute(
        DateOnly from,
        DateOnly toExclusive,
        RehearsalRule rule,
        IEnumerable<EventSpan> events,
        IEnumerable<CancellationSpan> cancellations)
    {
        var blocked = events
            .Where(e => !e.IsCancelled)
            .Select(CoveredDays)
            .ToList();

        // Užší výjimka vyhrává: „Hody“ uvnitř „Prázdnin“ nesou konkrétnější důvod.
        var exceptions = cancellations
            .OrderBy(c => c.DateTo.DayNumber - c.DateFrom.DayNumber)
            .ThenBy(c => c.Id)
            .ToList();

        var result = new List<RehearsalOccurrence>();
        for (var day = FirstOnOrAfter(from, rule.DayOfWeek); day < toExclusive; day = day.AddDays(7))
        {
            var d = day;
            if (blocked.Any(b => b.First <= d && d <= b.Last))
                continue;

            var startsAt = d.ToDateTime(TimeOnly.MinValue) + rule.StartTime;
            var endsAt = startsAt + rule.Duration;
            var cancellation = exceptions.FirstOrDefault(c => c.DateFrom <= d && d <= c.DateTo);
            var isCancelled = cancellation != default;

            result.Add(new RehearsalOccurrence(
                d,
                startsAt,
                endsAt,
                TimeZoneInfo.ConvertTimeToUtc(startsAt, rule.TimeZone),
                TimeZoneInfo.ConvertTimeToUtc(endsAt, rule.TimeZone),
                isCancelled ? RehearsalState.Cancelled : RehearsalState.Scheduled,
                isCancelled ? cancellation.Id : null,
                isCancelled ? cancellation.Reason : null));
        }

        return result;
    }

    /// <summary>
    /// Dny, přes které akce vede, včetně obou krajů.
    /// </summary>
    /// <remarks>
    /// Konec v 00:00 znamená dvě různé věci. Akce bez času (Blazor zadává jen
    /// datum od–do, obojí o půlnoci) má poslední den včetně, takže „čt–pá“ pátek
    /// zabírá. Akce s časem začátku končící o půlnoci („čt 18:00 – pá 00:00“)
    /// do pátku nezasahuje, konec je exkluzivní.
    /// </remarks>
    public static (DateOnly First, DateOnly Last) CoveredDays(EventSpan e)
    {
        var first = DateOnly.FromDateTime(e.DateFrom);
        var dateOnlyEvent = e.DateFrom.TimeOfDay == TimeSpan.Zero && e.DateTo.TimeOfDay == TimeSpan.Zero;
        var last = !dateOnlyEvent && e.DateTo.TimeOfDay == TimeSpan.Zero
            ? DateOnly.FromDateTime(e.DateTo).AddDays(-1)
            : DateOnly.FromDateTime(e.DateTo);

        // Konec před začátkem projde přes Blazor bez kontroly. Akce pak zabírá aspoň svůj první den.
        return (first, last < first ? first : last);
    }

    private static DateOnly FirstOnOrAfter(DateOnly date, DayOfWeek dayOfWeek)
    {
        var offset = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(offset);
    }
}
