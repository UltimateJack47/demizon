using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Attendance;

public class AttendanceReportService(DemizonContext db) : IAttendanceReportService
{
    public async Task<List<MemberAttendanceStat>> GetMemberStatsAsync(DateTime dateFrom, DateTime dateTo)
    {
        // Docházka na zrušenou zkoušku v databázi zůstává (vrátí se, když se
        // výjimka smaže), ale dokud výjimka platí, nepočítá se.
        var fromDay = DateOnly.FromDateTime(dateFrom);
        var toDay = DateOnly.FromDateTime(dateTo);
        var cancellations = await db.RehearsalCancellations
            .Where(c => c.DateFrom <= toDay && c.DateTo >= fromDay)
            .Select(c => new { c.DateFrom, c.DateTo })
            .ToListAsync();

        bool IsCancelledRehearsal(DateTime date, int? eventId)
        {
            if (eventId is not null) return false;
            var day = DateOnly.FromDateTime(date);
            return cancellations.Any(c => c.DateFrom <= day && day <= c.DateTo);
        }

        var rehearsalDays = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.EventId == null)
            .Select(a => a.Date.Date)
            .Distinct()
            .ToListAsync();
        var totalRehearsals = rehearsalDays.Count(d => !IsCancelledRehearsal(d, null));

        var totalActions = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.EventId != null)
            .Select(a => a.EventId)
            .Distinct()
            .CountAsync();

        var records = (await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.Member.IsAttendanceVisible)
            .Select(a => new { a.MemberId, a.Member.Name, a.Member.Surname, a.Status, a.EventId, a.Date })
            .ToListAsync())
            .Where(a => !IsCancelledRehearsal(a.Date, a.EventId))
            .ToList();

        return records
            .GroupBy(a => a.MemberId)
            .Select(g =>
            {
                var first = g.First();
                var attendedRehearsals = g.Count(x => x.Status == AttendanceStatus.Yes && x.EventId == null);
                var attendedActions = g.Count(x => x.Status == AttendanceStatus.Yes && x.EventId != null);
                return new MemberAttendanceStat(
                    g.Key,
                    $"{first.Name} {first.Surname}",
                    totalRehearsals,
                    attendedRehearsals,
                    totalRehearsals > 0 ? Math.Round((double)attendedRehearsals / totalRehearsals * 100, 1) : 0,
                    totalActions,
                    attendedActions,
                    totalActions > 0 ? Math.Round((double)attendedActions / totalActions * 100, 1) : 0
                );
            })
            .OrderByDescending(s => s.RehearsalRate)
            .ToList();
    }
}
