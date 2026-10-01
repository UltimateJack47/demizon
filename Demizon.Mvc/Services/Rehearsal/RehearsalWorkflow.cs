using Demizon.Common;
using Demizon.Common.Exceptions;
using Demizon.Core.Services.Attendance;
using Demizon.Core.Services.GoogleCalendar;
using Demizon.Core.Services.Member;
using Demizon.Core.Services.Rehearsal;
using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Dal.Extensions;
using Demizon.Mvc.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Mvc.Services.Rehearsal;

/// <summary>
/// Důsledky změn kalendáře zkoušek mimo databázi: Google Calendar členů a push.
/// Volá ho API i Blazor admin, aby se obojí chovalo stejně.
/// </summary>
/// <remarks>
/// Kalendář zkoušek mění čtyři věci: výjimka (zrušení, obnovení) a akce
/// (založení, úprava, smazání, zrušení). Všechny končí v
/// <see cref="SyncCalendarAsync"/>, který pro dotčené dny srovná Google
/// události s tím, jestli je v ten den zkouška.
/// </remarks>
public sealed class RehearsalWorkflow(
    DemizonContext db,
    IRehearsalService rehearsals,
    IAttendanceService attendances,
    IGoogleCalendarService calendar,
    IMemberService members,
    AttendanceReminderService sender,
    ILogger<RehearsalWorkflow> logger)
{
    /// <summary>Stejný název jako při vyplnění docházky (API i Blazor).</summary>
    public static string CalendarTitle(DateTime day) => $"Zkouška Demizon – {day:d. M. yyyy}";

    // ------------------------------------------------------------ výjimky

    /// <summary>
    /// Zruší zkoušky v rozsahu, smaže jejich Google události a volitelně pošle push.
    /// </summary>
    public async Task<Result<RehearsalCancelOutcome>> CancelAsync(
        DateOnly from, DateOnly to, string? reason, int? byMemberId, bool notify,
        CancellationToken ct = default)
    {
        // Dny, které se zrušením opravdu mění. Už zrušené (jinou výjimkou) nikdo
        // znovu upozorňovat nemá a jejich události v kalendáři už nejsou.
        var affected = to < from
            ? []
            : (await rehearsals.GetRangeAsync(from, to.AddDays(1), ct))
                .Where(o => !o.IsCancelled)
                .ToList();

        var created = await rehearsals.CancelAsync(from, to, reason, byMemberId, ct);
        if (!created.IsSuccess)
            return Result<RehearsalCancelOutcome>.Fail(created.Error!, created.ErrorKind);

        var days = affected.Select(o => o.Date).ToList();
        await SyncCalendarAsync(days, ct);

        var notified = notify && days.Count > 0
            ? await NotifyRehearsalsCancelledAsync(days, reason, ct)
            : 0;

        return Result<RehearsalCancelOutcome>.Ok(new RehearsalCancelOutcome(created.Value, notified));
    }

    /// <summary>Smaže výjimku a vrátí do Google Calendaru zkoušky, na které člen řekl „ano“.</summary>
    public async Task<Result> RestoreAsync(int cancellationId, CancellationToken ct = default)
    {
        var cancellation = await rehearsals.GetCancellationAsync(cancellationId, ct);

        var restored = await rehearsals.RestoreAsync(cancellationId, ct);
        if (!restored.IsSuccess || cancellation is null)
            return restored;

        await SyncCalendarAsync(Days(cancellation.DateFrom, cancellation.DateTo), ct);
        return restored;
    }

    // ------------------------------------------------------------ akce

    /// <summary>
    /// Akce se založila, změnila nebo smazala. Pátky, přes které vede (vedla),
    /// mohly zkoušku ztratit nebo získat, a Google události se jim srovnají.
    /// </summary>
    /// <remarks>
    /// Opravuje i starou chybu: akce založená na pátek, na který už měl někdo
    /// zkoušku „ano“, nechávala v Google viset „Zkouška Demizon“.
    /// </remarks>
    public async Task EventChangedAsync(EventSpan? before, EventSpan? after, CancellationToken ct = default)
    {
        var days = new HashSet<DateOnly>();
        foreach (var span in new[] { before, after })
        {
            if (span is null) continue;
            var (first, last) = RehearsalSchedule.CoveredDays(span.Value);
            days.UnionWith(Days(first, last));
        }

        await SyncCalendarAsync(days, ct);
    }

    /// <summary>
    /// Push členům, kteří na akci nemají „ne“. U akce, místo které teď je
    /// zkouška, to text řekne.
    /// </summary>
    /// <returns>Kolik členů push opravdu dostalo (měli zařízení).</returns>
    public async Task<int> NotifyEventCancelledAsync(int eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null || !ev.IsCancelled)
            return 0;

        var (first, last) = RehearsalSchedule.CoveredDays(new EventSpan(ev.DateFrom, ev.DateTo, ev.IsCancelled));
        var replacement = (await rehearsals.GetRangeAsync(first, last.AddDays(1), ct))
            .FirstOrDefault(o => !o.IsCancelled);

        var body = $"{ev.Name} – {ev.DateFrom:d. M. yyyy}";
        if (replacement is not null)
            body += $". Místo ní je zkouška {replacement.StartsAt:d. M.} v {replacement.StartsAt:H:mm}.";

        var declined = await db.Attendances
            .Where(a => a.EventId == eventId && a.Status == AttendanceStatus.No)
            .Select(a => a.MemberId)
            .ToListAsync(ct);
        var recipients = await db.Members
            .Where(m => !declined.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var data = new Dictionary<string, string> { ["eventId"] = eventId.ToString() };
        return await SendAsync(recipients, "Akce odpadá", body, data,
            memberId => new SentNotification
            {
                MemberId = memberId,
                EventId = eventId,
                NotificationType = NotificationType.EventCancelled,
                SentAt = DateTime.UtcNow,
            }, ct);
    }

    // ------------------------------------------------------------ Google Calendar

    /// <summary>
    /// Pro každý den srovná Google události docházky na zkoušku se stavem kalendáře:
    /// den se zkouškou má mít událost u každého „ano“, den bez zkoušky (akce,
    /// zrušení) žádnou. Selhání u jednoho člena nezastaví ostatní a neshodí volajícího —
    /// hlavní operace už proběhla.
    /// </summary>
    internal async Task SyncCalendarAsync(IReadOnlyCollection<DateOnly> days, CancellationToken ct)
    {
        if (days.Count == 0)
            return;

        var keys = days.Select(d => d.ToDateTime(TimeOnly.MinValue)).ToList();
        var rows = await db.Attendances
            .AsNoTracking()
            .Include(a => a.Member)
            .Where(a => a.EventId == null && keys.Contains(a.Date))
            .Where(a => a.Member.GoogleRefreshToken != null && a.Member.GoogleRefreshToken != ""
                        && a.Member.GoogleCalendarId != null && a.Member.GoogleCalendarId != "")
            .ToListAsync(ct);
        if (rows.Count == 0)
            return;

        var scheduled = (await rehearsals.GetRangeAsync(days.Min(), days.Max().AddDays(1), ct))
            .Where(o => !o.IsCancelled)
            .ToDictionary(o => o.Date);

        var revoked = new HashSet<int>();
        foreach (var att in rows)
        {
            if (revoked.Contains(att.MemberId))
                continue;

            var member = att.Member;
            var isRehearsal = scheduled.ContainsKey(DateOnly.FromDateTime(att.Date));
            try
            {
                if (!isRehearsal && att.GoogleEventId is not null)
                    await RemoveEventAsync(att, member);
                else if (isRehearsal && att.GoogleEventId is null && att.Status == AttendanceStatus.Yes)
                    await AddEventAsync(att, member);
            }
            catch (GoogleTokenRevokedException)
            {
                logger.LogWarning("Google token pro člena {MemberId} byl odvolán — mažu uložené credentials.", att.MemberId);
                revoked.Add(att.MemberId);
                await members.DisconnectGoogleCalendarAsync(att.MemberId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Srovnání Google Calendar selhalo pro docházku {AttendanceId}.", att.Id);
            }
        }
    }

    private async Task RemoveEventAsync(Attendance att, Member member)
    {
        var deleted = await calendar.DeleteEventAsync(
            member.GoogleRefreshToken!, member.GoogleCalendarId!, att.GoogleEventId!);
        if (!deleted)
            return; // ID nechat: smazání jde zkusit znovu, osiřelé ID by událost ztratilo napořád

        var cleared = await attendances.ClearGoogleEventIdAsync(att.Id);
        if (!cleared.IsSuccess)
        {
            logger.LogError("Docházka {AttendanceId} si drží ID smazané GCal události {GoogleEventId}: {Error}",
                att.Id, att.GoogleEventId, cleared.Error);
        }
    }

    private async Task AddEventAsync(Attendance att, Member member)
    {
        var googleEventId = await calendar.CreateEventAsync(
            member.GoogleRefreshToken!, member.GoogleCalendarId!, att.Date, null, CalendarTitle(att.Date));
        if (googleEventId is null)
            return;

        var linked = await attendances.SetGoogleEventIdAsync(att.Id, googleEventId);
        if (linked.IsSuccess)
            return;

        // Bez ID v docházce by šla událost smazat jen ručně v Google. Raději hned pryč.
        logger.LogError("Docházka {AttendanceId}: ID nové GCal události se nezapsalo ({Error}), ruším ji.",
            att.Id, linked.Error);
        await calendar.DeleteEventAsync(member.GoogleRefreshToken!, member.GoogleCalendarId!, googleEventId);
    }

    // ------------------------------------------------------------ push

    private async Task<int> NotifyRehearsalsCancelledAsync(
        IReadOnlyList<DateOnly> days, string? reason, CancellationToken ct)
    {
        var first = days.Min();
        var last = days.Max();
        var keys = days.Select(d => d.ToDateTime(TimeOnly.MinValue)).ToList();

        // „Nemají ne“ (rozhodnutí 5): kdo má na všechny zrušené dny „ne“, o zrušení ví.
        var declinedDays = await db.Attendances
            .Where(a => a.EventId == null && keys.Contains(a.Date) && a.Status == AttendanceStatus.No)
            .GroupBy(a => a.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Select(a => a.Date).Distinct().Count() })
            .ToListAsync(ct);
        var declinedAll = declinedDays.Where(d => d.Count >= days.Count).Select(d => d.MemberId).ToList();
        var recipients = await db.Members
            .Where(m => !declinedAll.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var (title, body) = days.Count == 1
            ? ("Zkouška odpadá", $"Zkouška {first:d. M. yyyy}")
            : ("Zkoušky odpadají", $"Zkoušky {first:d. M.} – {last:d. M. yyyy}");
        if (reason is not null)
            body += $" – {reason}";

        var firstDay = first.ToDateTime(TimeOnly.MinValue);
        var data = new Dictionary<string, string> { ["rehearsalDate"] = first.ToString("yyyy-MM-dd") };
        return await SendAsync(recipients, title, body, data,
            memberId => new SentNotification
            {
                MemberId = memberId,
                RehearsalDate = firstDay,
                NotificationType = NotificationType.RehearsalCancelled,
                SentAt = DateTime.UtcNow,
            }, ct);
    }

    private async Task<int> SendAsync(
        IReadOnlyList<int> recipients, string title, string body, Dictionary<string, string> data,
        Func<int, SentNotification> record, CancellationToken ct)
    {
        // Zrušení už je uložené. Selhání pushe ho nesmí vrátit jako chybu,
        // admin by to zkusil znovu a narazil na „už zrušeno“.
        var reached = 0;
        try
        {
            foreach (var memberId in recipients)
            {
                if (await sender.SendToMemberAsync(memberId, title, body, data, ct))
                    reached++;
                db.SentNotifications.Add(record(memberId));
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.DiscardPendingChanges();
            logger.LogError(ex, "Push „{Title}“ se nepodařilo rozeslat.", title);
        }

        return reached;
    }

    /// <remarks>
    /// Strop jako u výjimek: akce s překlepem v roce konce by jinak poslala
    /// do dotazu tisíce dní.
    /// </remarks>
    private static List<DateOnly> Days(DateOnly first, DateOnly last)
    {
        var days = new List<DateOnly>();
        for (var d = first; d <= last && days.Count < RehearsalService.MaxCancellationDays; d = d.AddDays(1))
            days.Add(d);
        return days;
    }
}

/// <param name="NotifiedCount">Kolik členů push opravdu dostalo (měli zařízení).</param>
public sealed record RehearsalCancelOutcome(int CancellationId, int NotifiedCount);
