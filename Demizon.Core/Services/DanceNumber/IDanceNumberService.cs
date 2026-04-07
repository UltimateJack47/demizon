using Demizon.Dal.Entities;

namespace Demizon.Core.Services.DanceNumber;

public interface IDanceNumberService
{
    IQueryable<Dal.Entities.DanceNumber> GetAllForDance(int danceId);
    Task<Dal.Entities.DanceNumber> GetOneAsync(int id);
    Task CreateAsync(Dal.Entities.DanceNumber danceNumber);
    Task UpdateAsync(int id, Dal.Entities.DanceNumber updated);
    Task DeleteAsync(int id);
}
