namespace DomProject.Core.Services.Device;

public interface IDeviceService
{
    Task<Dal.Entities.Device> GetOneAsync(int id);
    IQueryable<Dal.Entities.Device> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Device updatedDevice);
    Task<bool> CreateAsync(Dal.Entities.Device device);
    Task<bool> DeleteAsync(int id);
}
