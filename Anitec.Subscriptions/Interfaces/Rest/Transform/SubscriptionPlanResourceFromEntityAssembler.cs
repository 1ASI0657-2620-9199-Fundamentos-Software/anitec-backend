using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Interfaces.Rest.Resources;

namespace Anitec.Subscriptions.Interfaces.Rest.Transform;

public static class SubscriptionPlanResourceFromEntityAssembler
{
    public static SubscriptionPlanResource ToResourceFromEntity(SubscriptionPlan entity)
    {
        return new SubscriptionPlanResource(entity.Id, entity.Name, entity.Price, entity.StripePriceId, entity.MaxAnimals, entity.IsActive);
    }
}