namespace Anitec.Subscriptions.Infrastructure.Payments.Stripe;

/// <summary>
///     Stripe configuration. The values come from environment variables (StripeSettings__SecretKey,
///     StripeSettings__WebhookSecret, StripeSettings__SuccessUrl and StripeSettings__CancelUrl) and never from
///     the repository. Only test-mode keys (sk_test_...) are meant to be used.
/// </summary>
public class StripeSettings
{
    public string? SecretKey { get; set; }
    public string? WebhookSecret { get; set; }

    /// <summary>Page of the frontend that opens after paying. It must contain {CHECKOUT_SESSION_ID}.</summary>
    public string? SuccessUrl { get; set; }

    /// <summary>Page of the frontend that opens when the user cancels the payment.</summary>
    public string? CancelUrl { get; set; }
}
