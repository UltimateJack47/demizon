using DomProject.Common.Exceptions;
using DomProject.Dal;
using DomProject.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace DomProject.Core.Services;

public class DeviceService : IDeviceService
{
    private DomProjectContext DomProjectContext { get; set; }

    public DeviceService(DomProjectContext domProjectContext)
    {
        DomProjectContext = domProjectContext;
    }

    public async Task<Device> GetOneAsync(int id)
    {
        return await DomProjectContext.Devices.FindAsync(id) ?? throw new EntityNotFoundException($"Product with id: {id} not found.");
    }
    
    public IQueryable<Device> GetAll()
    {
        return DomProjectContext.Devices.AsQueryable();
    }

    public async Task UpdateAsync(int id, Device updatedDevice)
    {
        var entity = await DomProjectContext.Devices.FindAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException($"Product with id: {id} not found.");
        }
        DomProjectContext.Entry(entity).CurrentValues.SetValues(updatedDevice);
        DomProjectContext.Entry(entity).State = EntityState.Modified;
        await DomProjectContext.SaveChangesAsync();
    }

    public async Task<bool> CreateAsync(Device device)
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
