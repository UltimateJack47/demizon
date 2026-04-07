using Demizon.Dal;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Core.Services.Notification;

public class PushSubscriptionService(DemizonContext context) : IPushSubscriptionService
{
    public async Task<List<PushSubscription>> GetByMemberAsync(int memberId) =>
        await context.PushSubscriptions
            .Where(x => x.MemberId == memberId)
            .ToListAsync();

    public async Task<PushSubscription?> FindAsync(int memberId, string endpoint) =>
        await context.PushSubscriptions
            .FirstOrDefaultAsync(x => x.MemberId == memberId && x.Endpoint == endpoint);

    public async Task AddAsync(PushSubscription subscription)
    {
        // Zabrání duplicitní registraci stejného endpointu
        var existing = await FindAsync(subscription.MemberId, subscription.Endpoint);
        if (existing is not null) return;

        await context.PushSubscriptions.AddAsync(subscription);
        await context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int memberId, string endpoint)
    {
        var sub = await FindAsync(memberId, endpoint);
        if (sub is null) return;

        context.PushSubscriptions.Remove(sub);
        await context.SaveChangesAsync();
    }

    public async Task<List<PushSubscription>> GetAllAsync() =>
        await context.PushSubscriptions.ToListAsync();
}
