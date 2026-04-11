using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demizon.Core.Services.Member;

public class MemberService(DemizonContext demizonContext, ILogger<MemberService> logger) : IMemberService
{
    private DemizonContext DemizonContext { get; set; } = demizonContext;

    public async Task<Dal.Entities.Member> GetOneAsync(int id)
    {
        return await DemizonContext.Members.FindAsync(id) ??
               throw new EntityNotFoundException($"Member with id: {id} not found.");
    }

    public Dal.Entities.Member? GetOneByLogin(string? login)
    {
        return DemizonContext.Members.FirstOrDefault(x => x.Login == login);
    }

    public IQueryable<Dal.Entities.Member> GetAll()
    {
        return DemizonContext.Members.Where(x => x.DeletedAt == null);
    }

    public async Task UpdateAsync(int id, Dal.Entities.Member updatedMember)
    {
        var entity = await DemizonContext.Members.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Member with id: {id} not found.");
        }

        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedMember);
        entity.Photos = updatedMember.Photos;
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Member operation.");
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

            // Soft delete – data pro historii docházky zůstanou, global query filter skryje člena
            entity.DeletedAt = DateTime.UtcNow;
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Member operation.");
            return false;
        }
    }
}
