using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Shared.Domain.Repositories;

namespace Anitec.Subscriptions.Domain.Repositories;

public interface ISubscriptionRepository : IBaseRepository<Subscription>
{
    /// <summary>Lists the subscriptions of one user, newest first.</summary>
    Task<IEnumerable<Subscription>> FindByUserIdAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Finds the newest subscription of one user whose status is Active.</summary>
    Task<Subscription?> FindActiveByUserIdAsync(int userId, CancellationToken cancellationToken);
}
