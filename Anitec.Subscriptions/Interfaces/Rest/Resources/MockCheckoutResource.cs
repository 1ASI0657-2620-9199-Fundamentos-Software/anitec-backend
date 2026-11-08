namespace Anitec.Subscriptions.Interfaces.Rest.Resources;

/// <summary>Development-only request: the buyer comes from the token and the amount from the plan.</summary>
public record MockCheckoutResource(int PlanId);
