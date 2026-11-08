using Anitec.Subscriptions.Application.QueryServices;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Model.Queries;
using Anitec.Subscriptions.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.QueryServices;

public class SubscriptionPlanQueryService(ISubscriptionPlanRepository repository) : ISubscriptionPlanQueryService
{
    public async Task<SubscriptionPlan?> Handle(GetSubscriptionPlanByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<SubscriptionPlan>> Handle(GetAllSubscriptionPlansQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }
}




