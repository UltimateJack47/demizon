using Demizon.Dal.Entities;

namespace Demizon.Core.Services.Notification;

public interface IPushSubscriptionService
{
    Task<List<PushSubscription>> GetByMemberAsync(int memberId);
    Task<PushSubscription?> FindAsync(int memberId, string endpoint);
    Task AddAsync(PushSubscription subscription);
    Task RemoveAsync(int memberId, string endpoint);
    Task<List<PushSubscription>> GetAllAsync();
}
