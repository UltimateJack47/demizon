namespace Demizon.Core.Services.File;

public interface IFileService
{
    /// <summary>Metadata only — <see cref="Dal.Entities.File.Data"/> / ThumbnailData are not loaded.</summary>
    Task<Dal.Entities.File> GetOneAsync(int id);

    IQueryable<Dal.Entities.File> GetAll();

    /// <summary>Loads a single BLOB column. Does not materialize the other.</summary>
    Task<byte[]?> GetContentAsync(int id, bool thumbnail = false);

    Task UpdateAsync(int id, Dal.Entities.File updatedMember);
    Task<bool> CreateAsync(Dal.Entities.File file);
    Task<bool> DeleteAsync(int id);
}
