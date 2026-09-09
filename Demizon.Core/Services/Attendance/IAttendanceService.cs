namespace Demizon.Core.Services.Attendance;

public interface IAttendanceService
{
    Task<Dal.Entities.Attendance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Attendance> GetAll();
    /// <summary>Vloží nebo přepíše docházku. <c>Value</c> je klíč uloženého řádku.</summary>
    Task<Common.Result<int>> CreateOrUpdateAsync(Dal.Entities.Attendance attendance);
    Task<Common.Result> DeleteAsync(int id);

    /// <summary>
    /// Vynuluje <c>GoogleEventId</c> přímo v databázi, bez change trackeru.
    /// Používá se k dorovnání stavu po neúspěšném uložení: událost už
    /// v kalendáři není, ale docházka si její ID drží.
    /// </summary>
    Task<Common.Result> ClearGoogleEventIdAsync(int id);
    Task<List<Dal.Entities.Attendance>> GetMemberAttendancesAsync(int memberId, DateTime dateFrom, DateTime dateTo);
    Task<List<Dal.Entities.Attendance>> GetMembersAttendancesAsync(List<int> memberIds, DateTime dateFrom, DateTime dateTo);
}
