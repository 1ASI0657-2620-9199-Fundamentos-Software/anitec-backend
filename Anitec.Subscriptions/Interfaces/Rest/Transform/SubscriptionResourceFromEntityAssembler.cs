using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Interfaces.Rest.Resources;

namespace Anitec.Subscriptions.Interfaces.Rest.Transform;

public static class SubscriptionResourceFromEntityAssembler
{
    public static SubscriptionResource ToResourceFromEntity(Subscription entity)
    {
        return new SubscriptionResource(entity.Id, entity.UserId, entity.PlanId, entity.StripeCustomerId, entity.StripeSubscriptionId, entity.Status, entity.StartedAt, entity.EndsAt);
    }
}