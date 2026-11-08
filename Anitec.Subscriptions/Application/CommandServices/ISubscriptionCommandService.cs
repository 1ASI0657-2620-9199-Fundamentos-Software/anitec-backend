using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Shared.Application.Model;

namespace Anitec.Subscriptions.Application.CommandServices;

public interface ISubscriptionCommandService
{
    Task<Result<Subscription>> Handle(CreateSubscriptionCommand command, CancellationToken cancellationToken);
    Task<Result<Subscription>> Handle(UpdateSubscriptionCommand command, CancellationToken cancellationToken);
    Task<Result> Handle(DeleteSubscriptionCommand command, CancellationToken cancellationToken);
}
