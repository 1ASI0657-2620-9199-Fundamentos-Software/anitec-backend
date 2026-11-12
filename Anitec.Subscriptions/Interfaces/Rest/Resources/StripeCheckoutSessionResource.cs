namespace Anitec.Subscriptions.Interfaces.Rest.Resources;

public record StripeCheckoutSessionResource(string SessionId, string CheckoutUrl);
