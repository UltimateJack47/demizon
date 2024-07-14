namespace Demizon.Core.Services.Attendance;

public interface IAttendanceService
{
    Task<Dal.Entities.Attendance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Attendance> GetAll();
    Task<bool> CreateOrUpdateAsync(Dal.Entities.Attendance attendance);
    Task<bool> DeleteAsync(int id);
    Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId);
}
