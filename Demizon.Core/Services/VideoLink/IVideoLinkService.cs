namespace Demizon.Core.Services.VideoLink;

public interface IVideoLinkService
{
    Task<Dal.Entities.VideoLink> GetOneAsync(int id);
    IQueryable<Dal.Entities.VideoLink> GetAll();
    Task UpdateAsync(int id, Dal.Entities.VideoLink updatedVideoLink);
    /// <summary>Uloží novou entitu. <c>Value</c> je vygenerovaný klíč.</summary>
    Task<Common.Result<int>> CreateAsync(Dal.Entities.VideoLink file);
    Task<Common.Result> DeleteAsync(int id);
}
