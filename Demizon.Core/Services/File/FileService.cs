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
        var row = await DemizonContext.Files.AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new
            {
                f.Id,
                f.Path,
                f.FileExtension,
                f.ContentType,
                f.FileSize,
                f.IsPublic,
                f.Kind,
                f.MemberId,
                f.DanceId,
                HasData = f.Data != null
            })
            .FirstOrDefaultAsync()
            ?? throw new EntityNotFoundException($"File with id: {id} not found.");

        return new Dal.Entities.File
        {
            Id = row.Id,
            Path = row.Path,
            FileExtension = row.FileExtension,
            ContentType = row.ContentType,
            FileSize = row.FileSize,
            IsPublic = row.IsPublic,
            Kind = row.Kind,
            MemberId = row.MemberId,
            DanceId = row.DanceId,
            HasStoredData = row.HasData
        };
    }

    public IQueryable<Dal.Entities.File> GetAll()
    {
        return DemizonContext.Files.AsQueryable();
    }

    public async Task<byte[]?> GetContentAsync(int id, bool thumbnail = false)
    {
        var query = DemizonContext.Files.AsNoTracking().Where(f => f.Id == id);
        if (thumbnail)
            return await query.Select(f => f.ThumbnailData ?? f.Data).FirstOrDefaultAsync();
        return await query.Select(f => f.Data).FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(int id, Dal.Entities.File updatedMember)
    {
        // ExecuteUpdate so we never load or overwrite Data/ThumbnailData.
        var affected = await DemizonContext.Files
            .Where(f => f.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Path, updatedMember.Path)
                .SetProperty(f => f.FileExtension, updatedMember.FileExtension)
                .SetProperty(f => f.ContentType, updatedMember.ContentType)
                .SetProperty(f => f.FileSize, updatedMember.FileSize)
                .SetProperty(f => f.IsPublic, updatedMember.IsPublic)
                .SetProperty(f => f.Kind, updatedMember.Kind)
                .SetProperty(f => f.MemberId, updatedMember.MemberId)
                .SetProperty(f => f.DanceId, updatedMember.DanceId));

        if (affected == 0)
            throw new EntityNotFoundException($"File with id: {id} not found.");
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
            var affected = await DemizonContext.Files.Where(f => f.Id == id).ExecuteDeleteAsync();
            if (affected == 0)
                throw new EntityNotFoundException();
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
