using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Model.Queries;

namespace Anitec.Subscriptions.Application.QueryServices;

public interface ISubscriptionQueryService
{
    Task<Subscription?> Handle(GetSubscriptionByIdQuery query, CancellationToken cancellationToken);
    Task<IEnumerable<Subscription>> Handle(GetAllSubscriptionsQuery query, CancellationToken cancellationToken);
    Task<IEnumerable<Subscription>> Handle(GetSubscriptionsByUserQuery query, CancellationToken cancellationToken);
    Task<Subscription?> Handle(GetActiveSubscriptionByUserQuery query, CancellationToken cancellationToken);
}
