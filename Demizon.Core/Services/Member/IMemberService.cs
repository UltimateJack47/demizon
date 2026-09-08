namespace Demizon.Core.Services.Member;

public interface IMemberService
{
    Task<Dal.Entities.Member> GetOneAsync(int id);
    Dal.Entities.Member? GetOneByLogin(string? login);
    IQueryable<Dal.Entities.Member> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Member updatedMember);
    /// <summary>Uloží novou entitu. <c>Value</c> je vygenerovaný klíč.</summary>
    Task<Common.Result<int>> CreateAsync(Dal.Entities.Member member);
    Task<Common.Result> DeleteAsync(int id);
    Task ConnectGoogleCalendarAsync(int memberId, string refreshToken, string calendarId);
    Task DisconnectGoogleCalendarAsync(int memberId);
}
