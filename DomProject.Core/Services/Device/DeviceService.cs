using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.Device;

public class DeviceService : IDeviceService
{
    private DomProjectContext DomProjectContext { get; set; }

    public DeviceService(DomProjectContext domProjectContext)
    {
        DomProjectContext = domProjectContext;
    }

    public async Task<Dal.Entities.Device> GetOneAsync(int id)
    {
        return await DomProjectContext.Devices.FindAsync(id) ?? throw new EntityNotFoundException($"Device with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Device> GetAll()
    {
        return DomProjectContext.Devices.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Device updatedDevice)
    {
        var entity = await DomProjectContext.Devices.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Device with id: {id} not found.");
        }
        DomProjectContext.Entry(entity).CurrentValues.SetValues(updatedDevice);
        DomProjectContext.Entry(entity).State = EntityState.Modified;
        await DomProjectContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Device device)
    {
        try
        {
            await DomProjectContext.AddAsync(device);
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
            var entity = await DomProjectContext.Devices.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DomProjectContext.Devices.Remove(entity);
            await DomProjectContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
