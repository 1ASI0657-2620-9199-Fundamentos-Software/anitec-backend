using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Subscriptions.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class SubscriptionRepository(SubscriptionsDbContext context)
    : BaseRepository<Subscription>(context), ISubscriptionRepository
{
    public async Task<IEnumerable<Subscription>> FindByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await Context.Set<Subscription>()
            .Where(subscription => subscription.UserId == userId)
            .OrderByDescending(subscription => subscription.StartedAt)
            .ThenByDescending(subscription => subscription.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Subscription?> FindActiveByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await Context.Set<Subscription>()
            .Where(subscription => subscription.UserId == userId && subscription.Status == "Active")
            .OrderByDescending(subscription => subscription.StartedAt)
            .ThenByDescending(subscription => subscription.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
