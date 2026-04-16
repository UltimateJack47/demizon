using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Attendance;

public class AttendanceReportService(DemizonContext db) : IAttendanceReportService
{
    public async Task<List<MemberAttendanceStat>> GetMemberStatsAsync(DateTime dateFrom, DateTime dateTo)
    {
        var totalRehearsals = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.EventId == null)
            .Select(a => a.Date.Date)
            .Distinct()
            .CountAsync();

        var totalActions = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.EventId != null)
            .Select(a => a.EventId)
            .Distinct()
            .CountAsync();

        var records = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.Member.IsAttendanceVisible)
            .Select(a => new { a.MemberId, a.Member.Name, a.Member.Surname, a.Status, a.EventId })
            .ToListAsync();

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
