using Demizon.Common.Exceptions;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Dance;

public class DanceService(DemizonContext demizonContext, ILogger<DanceService> logger) : IDanceService
{
    private DemizonContext DemizonContext { get; } = demizonContext;

    public async Task<Dal.Entities.Dance> GetOneAsync(int id)
    {
        return await DemizonContext.Dances.FindAsync(id) ?? throw new EntityNotFoundException($"Dance with id: {id} not found.");
    }

    public IQueryable<Dal.Entities.Dance> GetAll()
    {
        return DemizonContext.Dances.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Dance updatedDance)
    {
        var entity = await DemizonContext.Dances.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Dance with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedDance);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesWithRecoveryAsync();
    }

    public async Task<Common.Result<int>> CreateAsync(Dal.Entities.Dance dance)
    {
        try
        {
            await DemizonContext.AddAsync(dance);
            await DemizonContext.SaveChangesAsync();
            return Common.Result<int>.Ok(dance.Id);
        }
        catch (Exception ex)
        {
            // Bez tohohle by rozpracovaná změna zůstala v trackeru a uložila se
            // při příštím — nesouvisejícím — SaveChanges v tomtéž Blazor okruhu,
            // takže vrácené false by nic nezaručovalo.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process Dance operation.");
            return Common.Result<int>.Fail("Tanec se nepodařilo uložit.");
        }
    }

    public async Task<Common.Result> DeleteAsync(int id)
    {
        var entity = await DemizonContext.Dances.FindAsync(id);
        if (entity is null)
        {
            // Chybějící řádek není výjimečná situace, jen odpověď „není co mazat".
            return Common.Result.NotFound("Tanec nebyl nalezen.");
        }

        try
        {
            DemizonContext.Dances.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return Common.Result.Ok();
        }
        catch (Exception ex)
        {
            // Vrátí tracker do čistého stavu, jinak by se smazání přehrálo
            // při příštím uložení v tomtéž okruhu.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process Dance operation.");
            return Common.Result.Fail("Tanec se nepodařilo smazat.");
        }
    }
}
