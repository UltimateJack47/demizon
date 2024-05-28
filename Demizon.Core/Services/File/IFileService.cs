namespace Demizon.Core.Services.File;

public interface IFileService
{
    Task<Dal.Entities.File> GetOneAsync(int id);
    IQueryable<Dal.Entities.File> GetAll();
    Task UpdateAsync(int id, Dal.Entities.File updatedMember);
    Task<bool> CreateAsync(Dal.Entities.File file);
    Task<bool> DeleteAsync(int id);
}
