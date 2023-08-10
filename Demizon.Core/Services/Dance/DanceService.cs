using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Dance;

public class DanceService : IDanceService
{
    private DemizonContext DemizonContext { get; set; }

    public DanceService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

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
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Dance dance)
    {
        try
        {
            await DemizonContext.AddAsync(dance);
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
            var entity = await DemizonContext.Dances.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Dances.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
