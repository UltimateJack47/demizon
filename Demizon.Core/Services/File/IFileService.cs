namespace Demizon.Core.Services.File;

public interface IFileService
{
    /// <summary>Metadata only — <see cref="Dal.Entities.File.Data"/> / ThumbnailData are not loaded.</summary>
    Task<Dal.Entities.File> GetOneAsync(int id);

    IQueryable<Dal.Entities.File> GetAll();

    /// <summary>Loads a single BLOB column. Does not materialize the other.</summary>
    Task<byte[]?> GetContentAsync(int id, bool thumbnail = false);

    Task UpdateAsync(int id, Dal.Entities.File updatedMember);
    /// <summary>Uloží novou entitu. <c>Value</c> je vygenerovaný klíč.</summary>
    Task<Common.Result<int>> CreateAsync(Dal.Entities.File file);
    Task<Common.Result> DeleteAsync(int id);
}
