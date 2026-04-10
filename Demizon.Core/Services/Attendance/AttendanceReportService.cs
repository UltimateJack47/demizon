using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Attendance;

public class AttendanceReportService(DemizonContext db) : IAttendanceReportService
{
    public async Task<List<MemberAttendanceStat>> GetMemberStatsAsync(DateTime dateFrom, DateTime dateTo)
    {
        // Načteme docházku všech viditelných a docházkových členů v daném období
        var records = await db.Attendances
            .Where(a => a.Date >= dateFrom && a.Date <= dateTo && a.Member.IsAttendanceVisible)
            .Select(a => new { a.MemberId, a.Member.Name, a.Member.Surname, a.Attends })
            .ToListAsync();

        return records
            .GroupBy(a => a.MemberId)
            .Select(g =>
            {
                var first = g.First();
                var attended = g.Count(x => x.Attends);
                var total = g.Count();
                return new MemberAttendanceStat(
                    g.Key,
                    $"{first.Name} {first.Surname}",
                    total,
                    attended,
                    total > 0 ? Math.Round((double)attended / total * 100, 1) : 0
                );
            })
            .OrderByDescending(s => s.AttendanceRate)
            .ToList();
    }
}
