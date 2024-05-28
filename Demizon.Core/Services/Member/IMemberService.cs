namespace Demizon.Core.Services.Member;

public interface IMemberService
{
    Task<Dal.Entities.Member> GetOneAsync(int id);
    Dal.Entities.Member? GetOneByLogin(string login);
    IQueryable<Dal.Entities.Member> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Member updatedMember);
    Task<bool> CreateAsync(Dal.Entities.Member member);
    Task<bool> DeleteAsync(int id);
}
