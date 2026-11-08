using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Interfaces.Rest.Resources;

namespace Anitec.Subscriptions.Interfaces.Rest.Transform;

public static class CreateSubscriptionPlanCommandFromResourceAssembler
{
    public static CreateSubscriptionPlanCommand ToCommandFromResource(CreateSubscriptionPlanResource resource)
    {
        return new CreateSubscriptionPlanCommand(resource.Name, resource.Price, resource.StripePriceId, resource.MaxAnimals, resource.IsActive);
    }
}