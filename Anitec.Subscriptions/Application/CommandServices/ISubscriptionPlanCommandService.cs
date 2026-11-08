using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Shared.Application.Model;

namespace Anitec.Subscriptions.Application.CommandServices;

public interface ISubscriptionPlanCommandService
{
    Task<Result<SubscriptionPlan>> Handle(CreateSubscriptionPlanCommand command, CancellationToken cancellationToken);
    Task<Result<SubscriptionPlan>> Handle(UpdateSubscriptionPlanCommand command, CancellationToken cancellationToken);
    Task<Result> Handle(DeleteSubscriptionPlanCommand command, CancellationToken cancellationToken);
}
