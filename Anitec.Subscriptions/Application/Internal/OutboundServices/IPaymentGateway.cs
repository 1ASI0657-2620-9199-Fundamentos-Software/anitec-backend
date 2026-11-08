namespace Anitec.Subscriptions.Application.Internal.OutboundServices;

/// <summary>
///     Outbound port to the payment provider. The application layer only knows this contract, so the flow can be
///     tested with a fake and the provider (Stripe in test mode today) can change without touching the services.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates a hosted checkout session and returns the page where the user pays.</summary>
    /// <exception cref="PaymentGatewayNotConfiguredException">The keys or the return URLs are not set.</exception>
    /// <exception cref="PaymentGatewayException">The provider rejected the request or could not be reached.</exception>
    Task<CheckoutSession> CreateCheckoutSessionAsync(CheckoutSessionRequest request, CancellationToken cancellationToken);

    /// <summary>Reads a checkout session from the provider, or returns <c>null</c> when it does not exist.</summary>
    /// <exception cref="PaymentGatewayNotConfiguredException">The keys are not set.</exception>
    /// <exception cref="PaymentGatewayException">The provider could not be reached.</exception>
    Task<CheckoutSessionDetails?> GetCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Verifies the signature of a webhook and returns the event it carries.</summary>
    /// <exception cref="PaymentGatewayNotConfiguredException">The webhook secret is not set.</exception>
    /// <exception cref="InvalidWebhookSignatureException">The signature is missing, wrong or the payload is malformed.</exception>
    PaymentWebhookEvent ParseWebhook(string payload, string? signatureHeader);
}
