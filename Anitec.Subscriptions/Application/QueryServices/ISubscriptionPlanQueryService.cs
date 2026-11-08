using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Model.Queries;

namespace Anitec.Subscriptions.Application.QueryServices;

public interface ISubscriptionPlanQueryService
{
    Task<SubscriptionPlan?> Handle(GetSubscriptionPlanByIdQuery query, CancellationToken cancellationToken);
    Task<IEnumerable<SubscriptionPlan>> Handle(GetAllSubscriptionPlansQuery query, CancellationToken cancellationToken);
}




