using Demizon.Common.Exceptions;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.File;

public class FileService(DemizonContext demizonContext, ILogger<FileService> logger) : IFileService
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
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.File file)
    {
        try
        {
            await DemizonContext.AddAsync(file);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Bez tohohle by entita zůstala Added a vložila se při příštím uložení
            // v tomtéž Blazor okruhu — vrácené false by nic nezaručovalo.
            DemizonContext.DiscardPendingChange(file);
            logger.LogError(ex, "Failed to process File operation.");
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Entita je deklarovaná mimo try, aby po selhání šla vrátit do Unchanged.
        Dal.Entities.File? entity = null;
        try
        {
            entity = await DemizonContext.Files.FindAsync(id);
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
            // Vrátí entitu do Unchanged, jinak by se smazání přehrálo při příštím uložení.
            DemizonContext.DiscardPendingChange(entity);
            logger.LogError(ex, "Failed to process File operation.");
            return false;
        }
    }
}
