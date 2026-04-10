namespace Demizon.Core.Services.Attendance;

public record MemberAttendanceStat(
    int MemberId,
    string FullName,
    int TotalRecords,
    int AttendedCount,
    double AttendanceRate
);

public interface IAttendanceReportService
{
    /// <summary>
    /// Vrátí statistiku docházky pro každého viditelného člena v daném období.
    /// </summary>
    Task<List<MemberAttendanceStat>> GetMemberStatsAsync(DateTime dateFrom, DateTime dateTo);
}
