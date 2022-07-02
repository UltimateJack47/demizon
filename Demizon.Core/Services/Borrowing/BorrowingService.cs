using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.Borrowing;

public class BorrowingService : IBorrowingService
{
    private DemizonContext DemizonContext { get; set; }

    public BorrowingService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

    public async Task<Dal.Entities.Borrowing> GetOneAsync(int id)
    {
        return await DemizonContext.Borrowings.FindAsync(id) ?? throw new EntityNotFoundException($"Borrowing with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Borrowing> GetAll()
    {
        return DemizonContext.Borrowings.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Borrowing updatedUser)
    {
        var entity = await DemizonContext.Borrowings.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Borrowing with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedUser);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Borrowing borrowing)
    {
        try
        {
            await DemizonContext.AddAsync(borrowing);
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
            var entity = await DemizonContext.Borrowings.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Borrowings.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SetReturned(int id)
    {
        try
        {
            var entity = await DemizonContext.Borrowings.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }

            entity.End = DateTime.UtcNow;
            DemizonContext.Borrowings.Update(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
