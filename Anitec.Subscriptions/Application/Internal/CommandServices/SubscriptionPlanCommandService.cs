using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Anitec.Subscriptions.Shared.Application.Model;
using Anitec.Subscriptions.Shared.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.CommandServices;

public class SubscriptionPlanCommandService(ISubscriptionPlanRepository repository, IUnitOfWork unitOfWork)
    : ISubscriptionPlanCommandService
{
    public async Task<Result<SubscriptionPlan>> Handle(CreateSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        var entity = new SubscriptionPlan(command);
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<SubscriptionPlan>.Success(entity);
    }

    public async Task<Result<SubscriptionPlan>> Handle(UpdateSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result<SubscriptionPlan>.Failure(SubscriptionsError.SubscriptionPlanNotFound, "SubscriptionPlan not found.");
        entity.Name = command.Name;
        entity.Price = command.Price;
        entity.StripePriceId = command.StripePriceId;
        entity.MaxAnimals = command.MaxAnimals;
        entity.IsActive = command.IsActive;

        repository.Update(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<SubscriptionPlan>.Success(entity);
    }

    public async Task<Result> Handle(DeleteSubscriptionPlanCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result.Failure(SubscriptionsError.SubscriptionPlanNotFound, "SubscriptionPlan not found.");
        repository.Remove(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result.Success();
    }
}
