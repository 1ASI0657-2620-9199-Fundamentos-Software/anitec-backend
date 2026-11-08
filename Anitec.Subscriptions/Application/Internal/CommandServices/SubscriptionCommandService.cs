using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Anitec.Subscriptions.Shared.Application.Model;
using Anitec.Subscriptions.Shared.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.CommandServices;

public class SubscriptionCommandService(ISubscriptionRepository repository, IUnitOfWork unitOfWork)
    : ISubscriptionCommandService
{
    public async Task<Result<Subscription>> Handle(CreateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var entity = new Subscription(command);
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Subscription>.Success(entity);
    }

    public async Task<Result<Subscription>> Handle(UpdateSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result<Subscription>.Failure(SubscriptionsError.SubscriptionNotFound, "Subscription not found.");
        entity.UserId = command.UserId;
        entity.PlanId = command.PlanId;
        entity.StripeCustomerId = command.StripeCustomerId;
        entity.StripeSubscriptionId = command.StripeSubscriptionId;
        entity.Status = command.Status;
        entity.StartedAt = command.StartedAt;
        entity.EndsAt = command.EndsAt;

        repository.Update(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Subscription>.Success(entity);
    }

    public async Task<Result> Handle(DeleteSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result.Failure(SubscriptionsError.SubscriptionNotFound, "Subscription not found.");
        repository.Remove(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result.Success();
    }
}
