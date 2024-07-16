namespace Demizon.Core.Services.Attendance;

public interface IAttendanceService
{
    Task<Dal.Entities.Attendance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Attendance> GetAll();
    Task<bool> CreateOrUpdateAsync(Dal.Entities.Attendance attendance);
    Task<bool> DeleteAsync(int id);
    Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId, DateTime dateFrom, DateTime dateTo);
    Task<List<Dal.Entities.Attendance>> GetMembersAttendancesAsync(List<int> memberIds, DateTime dateFrom, DateTime dateTo);
}
