using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.DanceNumber;

public class DanceNumberService(DemizonContext context) : IDanceNumberService
{
    public IQueryable<Dal.Entities.DanceNumber> GetAllForDance(int danceId)
    {
        return context.DanceNumbers.Where(x => x.DanceId == danceId).AsQueryable();
    }

    public async Task<Dal.Entities.DanceNumber> GetOneAsync(int id)
    {
        return await context.DanceNumbers.FindAsync(id)
               ?? throw new EntityNotFoundException($"DanceNumber with id: {id} not found.");
    }

    public async Task CreateAsync(Dal.Entities.DanceNumber danceNumber)
    {
        await context.DanceNumbers.AddAsync(danceNumber);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(int id, Dal.Entities.DanceNumber updated)
    {
        var entity = await context.DanceNumbers.FindAsync(id)
                     ?? throw new EntityNotFoundException($"DanceNumber with id: {id} not found.");
        context.Entry(entity).CurrentValues.SetValues(updated);
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await context.DanceNumbers.FindAsync(id)
                     ?? throw new EntityNotFoundException($"DanceNumber with id: {id} not found.");
        context.DanceNumbers.Remove(entity);
        await context.SaveChangesAsync();
    }
}
