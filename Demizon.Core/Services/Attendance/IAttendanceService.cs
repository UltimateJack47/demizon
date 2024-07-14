namespace Demizon.Core.Services.Attendance;

public interface IAttendanceService
{
    Task<Dal.Entities.Attendance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Attendance> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Attendance updatedAttendance);
    Task<bool> CreateAsync(Dal.Entities.Attendance attendance);
    Task<bool> DeleteAsync(int id);
    Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId);
}