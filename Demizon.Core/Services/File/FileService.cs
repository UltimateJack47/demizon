using Demizon.Common.Exceptions;
using Demizon.Core.Services.Storage;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.File;

public class FileService(
    DemizonContext demizonContext,
    IStorageQuotaService storageQuota,
    ILogger<FileService> logger) : IFileService
{
    private DemizonContext DemizonContext { get; set; } = demizonContext;

    public async Task<Dal.Entities.File> GetOneAsync(int id)
    {
        return await DemizonContext.Files.FindAsync(id) ?? throw new EntityNotFoundException($"File with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.File> GetAll()
    {
        return DemizonContext.Files.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.File updatedMember)
    {
        var entity = await DemizonContext.Files.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"File with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedMember);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesWithRecoveryAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.File file)
    {
        try
        {
            var storedBytes = file.FileSize
                              + (file.ThumbnailData?.LongLength ?? 0);
            var (allowed, reason) = await storageQuota.EnsureCanStoreAsync(storedBytes);
            if (!allowed)
            {
                logger.LogWarning("Upload rejected by storage quota: {Reason}", reason);
                return false;
            }

            await DemizonContext.AddAsync(file);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process File operation.");
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var entity = await DemizonContext.Files.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }

            DemizonContext.Files.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process File operation.");
            return false;
        }
    }
}
