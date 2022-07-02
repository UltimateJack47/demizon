using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.User;

public class UserService : IUserService
{
    private DemizonContext DemizonContext { get; set; }

    public UserService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

    public async Task<Dal.Entities.User> GetOneAsync(int id)
    {
        return await DemizonContext.Users.FindAsync(id) ?? throw new EntityNotFoundException($"User with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.User> GetAll()
    {
        return DemizonContext.Users.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.User updatedUser)
    {
        var entity = await DemizonContext.Users.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"User with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedUser);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.User user)
    {
        try
        {
            await DemizonContext.AddAsync(user);
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
            var entity = await DemizonContext.Users.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Users.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
