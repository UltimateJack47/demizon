using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.Member;

public class MemberService : IMemberService
{
    private DemizonContext DemizonContext { get; set; }

    public MemberService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

    public async Task<Dal.Entities.Member> GetOneAsync(int id)
    {
        return await DemizonContext.Members.FindAsync(id) ?? throw new EntityNotFoundException($"VideoLink with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Member> GetAll()
    {
        return DemizonContext.Members.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Member updatedMember)
    {
        var entity = await DemizonContext.Members.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Member with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedMember);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Member member)
    {
        try
        {
            await DemizonContext.AddAsync(member);
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
            var entity = await DemizonContext.Members.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Members.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
