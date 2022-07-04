namespace Demizon.Core.Services.VideoLink;

public interface IVideoLinkService
{
    Task<Dal.Entities.VideoLink> GetOneAsync(int id);
    IQueryable<Dal.Entities.VideoLink> GetAll();
    Task UpdateAsync(int id, Dal.Entities.VideoLink updatedVideoLink);
    Task<bool> CreateAsync(Dal.Entities.VideoLink file);
    Task<bool> DeleteAsync(int id);
}
