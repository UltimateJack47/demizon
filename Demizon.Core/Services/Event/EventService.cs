using Demizon.Common.Exceptions;
using Demizon.Dal;
using Demizon.Dal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Event;

public class EventService(DemizonContext demizonContext, ILogger<EventService> logger) : IEventService
{
    private DemizonContext DemizonContext { get; } = demizonContext;

    public async Task<Dal.Entities.Event> GetOneAsync(int id)
    {
        return await DemizonContext.Events.FindAsync(id) ?? throw new EntityNotFoundException($"Event with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Event> GetAll()
    {
        return DemizonContext.Events.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Event updatedEvent)
    {
        var entity = await DemizonContext.Events.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Event with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedEvent);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Event newEvent)
    {
        try
        {
            await DemizonContext.AddAsync(newEvent);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Bez tohohle by rozpracovaná změna zůstala v trackeru a uložila se
            // při příštím — nesouvisejícím — SaveChanges v tomtéž Blazor okruhu,
            // takže vrácené false by nic nezaručovalo.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to process Event operation.");
            return false;
        }
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await DemizonContext.Events.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Event with id: {id} not found.");
        }

        try
        {
            DemizonContext.Events.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            // Vrátí tracker do čistého stavu, jinak by se smazání přehrálo
            // při příštím uložení v tomtéž okruhu.
            DemizonContext.DiscardPendingChanges();
            logger.LogError(ex, "Failed to delete Event {EventId}.", id);
            return false;
        }
    }

    public async Task SetCancelledAsync(int id, bool isCancelled)
    {
        var entity = await DemizonContext.Events.FindAsync(id)
            ?? throw new EntityNotFoundException($"Event with id: {id} not found.");
        entity.IsCancelled = isCancelled;
        await DemizonContext.SaveChangesAsync();
    }
}
