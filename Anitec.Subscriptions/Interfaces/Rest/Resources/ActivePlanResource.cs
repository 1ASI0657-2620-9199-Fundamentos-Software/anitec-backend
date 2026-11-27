namespace Anitec.Subscriptions.Interfaces.Rest.Resources;

/// <summary>The plan of the active subscription of the caller and what it allows.</summary>
public record ActivePlanResource(int SubscriptionId, int PlanId, string PlanName, int MaxAnimals, string Status);
