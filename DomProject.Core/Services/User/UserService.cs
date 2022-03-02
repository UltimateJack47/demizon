using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.User;

public class UserService : IUserService
{
    private DomProjectContext DomProjectContext { get; set; }

    public UserService(DomProjectContext domProjectContext)
    {
        DomProjectContext = domProjectContext;
    }

    public async Task<Dal.Entities.User> GetOneAsync(int id)
    {
        return await DomProjectContext.Users.FindAsync(id) ?? throw new EntityNotFoundException($"User with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.User> GetAll()
    {
        return DomProjectContext.Users.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.User updatedUser)
    {
        var entity = await DomProjectContext.Users.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"User with id: {id} not found.");
        }
        DomProjectContext.Entry(entity).CurrentValues.SetValues(updatedUser);
        DomProjectContext.Entry(entity).State = EntityState.Modified;
        await DomProjectContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.User user)
    {
        try
        {
            await DomProjectContext.AddAsync(user);
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
            var entity = await DomProjectContext.Users.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DomProjectContext.Users.Remove(entity);
            await DomProjectContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
