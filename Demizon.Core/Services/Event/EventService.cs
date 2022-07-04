using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Event;

public class EventService : IEventService
{
    private DemizonContext DemizonContext { get; set; }

    public EventService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

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
        catch (Exception)
        {
            return false;
        }
    }
    
    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var entity = await DemizonContext.Events.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Events.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
