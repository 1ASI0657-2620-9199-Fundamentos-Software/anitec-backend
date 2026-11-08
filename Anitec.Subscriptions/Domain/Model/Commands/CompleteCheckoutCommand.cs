namespace Anitec.Subscriptions.Domain.Model.Commands;

/// <summary>Records a paid checkout. It is idempotent: the same session id is stored only once.</summary>
public record CompleteCheckoutCommand(string SessionId, int? UserId, int? PlanId, string? CustomerId, string? SubscriptionId);
