namespace Demizon.Core.Services.User;

public interface IUserService
{
    Task<Dal.Entities.User> GetOneAsync(int id);
    IQueryable<Dal.Entities.User> GetAll();
    Task UpdateAsync(int id, Dal.Entities.User updatedUser);
    Task<bool> CreateAsync(Dal.Entities.User user);
    Task<bool> DeleteAsync(int id);
}
