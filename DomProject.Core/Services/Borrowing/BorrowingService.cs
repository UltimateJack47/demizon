using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.Borrowing;

public class BorrowingService : IBorrowingService
{
    private DomProjectContext DomProjectContext { get; set; }

    public BorrowingService(DomProjectContext domProjectContext)
    {
        DomProjectContext = domProjectContext;
    }

    public async Task<Dal.Entities.Borrowing> GetOneAsync(int id)
    {
        return await DomProjectContext.Borrowings.FindAsync(id) ?? throw new EntityNotFoundException($"Borrowing with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Borrowing> GetAll()
    {
        return DomProjectContext.Borrowings.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Borrowing updatedUser)
    {
        var entity = await DomProjectContext.Borrowings.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Borrowing with id: {id} not found.");
        }
        DomProjectContext.Entry(entity).CurrentValues.SetValues(updatedUser);
        DomProjectContext.Entry(entity).State = EntityState.Modified;
        await DomProjectContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Borrowing borrowing)
    {
        try
        {
            await DomProjectContext.AddAsync(borrowing);
            await DomProjectContext.SaveChangesAsync();
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
            var entity = await DomProjectContext.Borrowings.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DomProjectContext.Borrowings.Remove(entity);
            await DomProjectContext.SaveChangesAsync();
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
            var entity = await DomProjectContext.Borrowings.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }

            entity.End = DateTime.UtcNow;
            DomProjectContext.Borrowings.Update(entity);
            await DomProjectContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
