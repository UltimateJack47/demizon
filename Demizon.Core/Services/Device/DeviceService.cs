using DomProject.Common.Exceptions;
using DomProject.Dal;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services.Device;

public class DeviceService : IDeviceService
{
    private DemizonContext DemizonContext { get; set; }

    public DeviceService(DemizonContext demizonContext)
    {
        DemizonContext = demizonContext;
    }

    public async Task<Dal.Entities.Device> GetOneAsync(int id)
    {
        return await DemizonContext.Devices.FindAsync(id) ?? throw new EntityNotFoundException($"Device with id: {id} not found.");
    }
    
    public IQueryable<Dal.Entities.Device> GetAll()
    {
        return DemizonContext.Devices.AsQueryable();
    }

    public async Task UpdateAsync(int id, Dal.Entities.Device updatedDevice)
    {
        var entity = await DemizonContext.Devices.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Device with id: {id} not found.");
        }
        DemizonContext.Entry(entity).CurrentValues.SetValues(updatedDevice);
        DemizonContext.Entry(entity).State = EntityState.Modified;
        await DemizonContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Dal.Entities.Device device)
    {
        try
        {
            await DemizonContext.AddAsync(device);
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
            var entity = await DemizonContext.Devices.FindAsync(id);
            if (entity is null)
            {
                throw new EntityNotFoundException();
            }
            
            DemizonContext.Devices.Remove(entity);
            await DemizonContext.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
