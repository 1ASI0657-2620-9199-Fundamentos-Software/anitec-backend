namespace Anitec.Subscriptions.Domain.Model.Commands;

public record UpdateSubscriptionPlanCommand(int Id, string Name, decimal Price, string StripePriceId, int MaxAnimals, bool IsActive);
