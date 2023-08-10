namespace Demizon.Core.Services.Dance;

public interface IDanceService
{
    Task<Dal.Entities.Dance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Dance> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Dance updatedDance);
    Task<bool> CreateAsync(Dal.Entities.Dance dance);
    Task<bool> DeleteAsync(int id);
}
