namespace Demizon.Core.Services.Dance;

public interface IDanceService
{
    Task<Dal.Entities.Dance> GetOneAsync(int id);
    IQueryable<Dal.Entities.Dance> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Dance updatedDance);
    /// <summary>Uloží novou entitu. <c>Value</c> je vygenerovaný klíč.</summary>
    Task<Common.Result<int>> CreateAsync(Dal.Entities.Dance dance);
    Task<Common.Result> DeleteAsync(int id);
}
