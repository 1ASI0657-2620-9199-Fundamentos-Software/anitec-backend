using Anitec.Subscriptions.Application.QueryServices;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Model.Queries;
using Anitec.Subscriptions.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.QueryServices;

public class SubscriptionQueryService(ISubscriptionRepository repository) : ISubscriptionQueryService
{
    public async Task<Subscription?> Handle(GetSubscriptionByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> Handle(GetAllSubscriptionsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Subscription>> Handle(GetSubscriptionsByUserQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByUserIdAsync(query.UserId, cancellationToken);
    }

    public async Task<Subscription?> Handle(GetActiveSubscriptionByUserQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindActiveByUserIdAsync(query.UserId, cancellationToken);
    }
}
