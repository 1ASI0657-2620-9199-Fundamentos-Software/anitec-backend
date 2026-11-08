namespace Anitec.Subscriptions.Application.Internal.OutboundServices;

/// <summary>What the gateway needs to open a checkout. The amount always comes from the plan stored in the service.</summary>
public record CheckoutSessionRequest(int UserId, int PlanId, string PlanName, int MaxAnimals, decimal Price, string Currency);

/// <summary>A checkout session just created: its id and the page where the user pays.</summary>
public record CheckoutSession(string Id, string Url);

/// <summary>The data of a checkout session that the service needs once the user has paid.</summary>
/// <param name="Id">Id of the session; it is the key of the payment.</param>
/// <param name="IsPaid">True only when the session is complete and the payment was collected.</param>
/// <param name="UserId">Buyer, read from the metadata the service wrote when it opened the session.</param>
/// <param name="PlanId">Plan, read from the same metadata.</param>
/// <param name="CustomerId">Customer id at the provider, when there is one.</param>
/// <param name="SubscriptionId">Subscription id at the provider, when there is one.</param>
public record CheckoutSessionDetails(
    string Id,
    bool IsPaid,
    int? UserId,
    int? PlanId,
    string? CustomerId,
    string? SubscriptionId);

/// <summary>An event received from the provider after its signature was verified.</summary>
public record PaymentWebhookEvent(string Id, string Type, CheckoutSessionDetails? Session);
