using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Interfaces.Rest.Resources;

namespace Anitec.Subscriptions.Interfaces.Rest.Transform;

public static class CreateSubscriptionCommandFromResourceAssembler
{
    public static CreateSubscriptionCommand ToCommandFromResource(CreateSubscriptionResource resource)
    {
        return new CreateSubscriptionCommand(resource.UserId, resource.PlanId, resource.StripeCustomerId, resource.StripeSubscriptionId, resource.Status, resource.StartedAt, resource.EndsAt);
    }
}