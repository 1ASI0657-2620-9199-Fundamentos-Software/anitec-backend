namespace Anitec.Subscriptions.Domain.Model.Commands;

public record CreateSubscriptionPlanCommand(string Name, decimal Price, string StripePriceId, int MaxAnimals, bool IsActive);
