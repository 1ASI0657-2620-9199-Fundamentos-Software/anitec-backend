using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Interfaces.Rest.Resources;

namespace Anitec.Subscriptions.Interfaces.Rest.Transform;

public static class UpdateSubscriptionPlanCommandFromResourceAssembler
{
    public static UpdateSubscriptionPlanCommand ToCommandFromResource(int id, CreateSubscriptionPlanResource resource)
    {
        return new UpdateSubscriptionPlanCommand(id, resource.Name, resource.Price, resource.StripePriceId, resource.MaxAnimals, resource.IsActive);
    }
}