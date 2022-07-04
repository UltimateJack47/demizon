namespace Demizon.Core.Services.Event;

public interface IEventService
{
    Task<Dal.Entities.Event> GetOneAsync(int id);
    IQueryable<Dal.Entities.Event> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Event updatedEvent);
    Task<bool> CreateAsync(Dal.Entities.Event newEvent);
    Task<bool> DeleteAsync(int id);
}
