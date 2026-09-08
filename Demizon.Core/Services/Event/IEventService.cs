namespace Demizon.Core.Services.Event;

public interface IEventService
{
    Task<Dal.Entities.Event> GetOneAsync(int id);
    IQueryable<Dal.Entities.Event> GetAll();
    Task UpdateAsync(int id, Dal.Entities.Event updatedEvent);
    /// <summary>Uloží novou entitu. <c>Value</c> je vygenerovaný klíč.</summary>
    Task<Common.Result<int>> CreateAsync(Dal.Entities.Event newEvent);
    Task<Common.Result> DeleteAsync(int id);
    Task SetCancelledAsync(int id, bool isCancelled);
}
