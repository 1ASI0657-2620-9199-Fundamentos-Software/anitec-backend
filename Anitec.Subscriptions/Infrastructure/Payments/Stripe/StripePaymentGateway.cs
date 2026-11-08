using System.Globalization;
using System.Net;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace Anitec.Subscriptions.Infrastructure.Payments.Stripe;

/// <summary>
///     Stripe implementation of the payment gateway. It keeps its own <see cref="StripeClient" />: nothing is written to
///     the static <c>StripeConfiguration</c>, so two instances never share a key by accident.
/// </summary>
public sealed class StripePaymentGateway(IOptions<StripeSettings> options) : IPaymentGateway
{
    private readonly object _clientLock = new();
    private readonly StripeSettings _settings = options.Value;
    private StripeClient? _client;

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var successUrl = RequireSetting(_settings.SuccessUrl, "StripeSettings:SuccessUrl");
        var cancelUrl = RequireSetting(_settings.CancelUrl, "StripeSettings:CancelUrl");

        var unitAmount = (long)Math.Round(request.Price * 100, MidpointRounding.AwayFromZero);
        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = request.UserId.ToString(CultureInfo.InvariantCulture),
            // The webhook trusts this metadata only because the signature proves Stripe sent it back.
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = request.UserId.ToString(CultureInfo.InvariantCulture),
                ["planId"] = request.PlanId.ToString(CultureInfo.InvariantCulture)
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = unitAmount,
                        Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "month" },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.PlanName,
                            Description = $"AniTec subscription plan for up to {request.MaxAnimals} animals"
                        }
                    }
                }
            ]
        };

        try
        {
            var session = await new SessionService(Client()).CreateAsync(sessionOptions,
                cancellationToken: cancellationToken);
            return new CheckoutSession(session.Id, session.Url);
        }
        catch (StripeException exception)
        {
            throw new PaymentGatewayException("Stripe could not create the checkout session.", exception);
        }
    }

    public async Task<CheckoutSessionDetails?> GetCheckoutSessionAsync(
        string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var session = await new SessionService(Client()).GetAsync(sessionId,
                cancellationToken: cancellationToken);
            return ToDetails(session);
        }
        catch (StripeException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (StripeException exception)
        {
            throw new PaymentGatewayException("Stripe could not return the checkout session.", exception);
        }
    }

    public PaymentWebhookEvent ParseWebhook(string payload, string? signatureHeader)
    {
        var webhookSecret = RequireSetting(_settings.WebhookSecret, "StripeSettings:WebhookSecret");
        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new InvalidWebhookSignatureException("The Stripe-Signature header is missing.");

        Event stripeEvent;
        try
        {
            // The events come from the account's API version, which can differ from the one of the library.
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Exception exception)
        {
            throw new InvalidWebhookSignatureException("The webhook signature could not be verified.", exception);
        }

        var details = stripeEvent.Data.Object is Session session ? ToDetails(session) : null;
        return new PaymentWebhookEvent(stripeEvent.Id, stripeEvent.Type, details);
    }

    private static CheckoutSessionDetails ToDetails(Session session)
    {
        var paymentCollected = session.PaymentStatus is "paid" or "no_payment_required";
        var isPaid = string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase) && paymentCollected;

        return new CheckoutSessionDetails(
            session.Id,
            isPaid,
            ReadInt(session.Metadata, "userId"),
            ReadInt(session.Metadata, "planId"),
            session.CustomerId,
            session.SubscriptionId);
    }

    private static int? ReadInt(IDictionary<string, string>? metadata, string key)
    {
        return metadata is not null
               && metadata.TryGetValue(key, out var value)
               && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private StripeClient Client()
    {
        if (_client is not null) return _client;

        lock (_clientLock)
        {
            return _client ??= new StripeClient(RequireSetting(_settings.SecretKey, "StripeSettings:SecretKey"));
        }
    }

    // An empty value, or the placeholder that older appsettings files carried, counts as not configured.
    private static string RequireSetting(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("change_me", StringComparison.OrdinalIgnoreCase))
            throw new PaymentGatewayNotConfiguredException($"{name} is not configured.");
        return value;
    }
}
