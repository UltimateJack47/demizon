using Demizon.Common.Exceptions;
using Demizon.Dal;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.VideoLink;

public class VideoLinkService : IVideoLinkService
{
    private DemizonContext DemizonContext { get; set; }

    public VideoLinkService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

    public async Task<Dal.Entities.VideoLink> GetOneAsync(int id)
    {
        return await DemizonContext.VideoLinks.FindAsync(id) ?? throw new EntityNotFoundException($"VideoLink with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.VideoLink> GetAll()
    {
        return DemizonContext.VideoLinks.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.VideoLink updatedVideoLink)
    {
        var entity = await DemizonContext.VideoLinks.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"File with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedVideoLink);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.VideoLink file)
    {
        try
        {
            await DemizonContext.AddAsync(file);
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
            var entity = await DemizonContext.VideoLinks.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.VideoLinks.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
