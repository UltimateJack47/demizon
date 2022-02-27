using DomProject.Dal.Entities;

namespace DomProject.Core.Services;

public interface IDeviceService
{
    Task<Device> GetOneAsync(int id);
    IQueryable<Device> GetAll();
    Task UpdateAsync(int id, Device updatedDevice);
    Task<bool> CreateAsync(Device device);
    Task<bool> DeleteAsync(int id);
}
