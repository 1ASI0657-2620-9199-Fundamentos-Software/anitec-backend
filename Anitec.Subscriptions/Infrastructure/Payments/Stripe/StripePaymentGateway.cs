using System.Globalization;
using System.Net;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Stripe;
using Stripe.Checkout;

namespace Anitec.Subscriptions.Infrastructure.Payments.Stripe;

/// <summary>
///     Stripe implementation of the payment gateway. It keeps its own <see cref="StripeClient" />: nothing is written to
///     the static <c>StripeConfiguration</c>, so two instances never share a key by accident.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly object _clientLock = new();
    private readonly Func<string, StripeClient> _clientFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly TimeSpan _breakDuration;
    private readonly StripeSettings _settings;
    private StripeClient? _client;

    /// <param name="options">Keys and return URLs of Stripe.</param>
    /// <param name="resilience">Timeout and circuit breaker of the calls. Optional: the defaults of the design apply.</param>
    /// <param name="clock">Clock of the circuit breaker. Optional; the tests move it by hand.</param>
    /// <param name="clientFactory">Builds the client of Stripe from the key. Optional; the tests pass a fake server.</param>
    public StripePaymentGateway(
        IOptions<StripeSettings> options,
        IOptions<StripeResilienceOptions>? resilience = null,
        TimeProvider? clock = null,
        Func<string, StripeClient>? clientFactory = null)
    {
        _settings = options.Value;
        var limits = resilience?.Value ?? new StripeResilienceOptions();
        _breakDuration = TimeSpan.FromSeconds(limits.BreakDurationSeconds);
        _pipeline = StripeResilience.Create(limits, clock);
        _clientFactory = clientFactory ?? (key => new StripeClient(key));
    }

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
            var service = new SessionService(Client());
            var session = await _pipeline.ExecuteAsync(
                async token => await service.CreateAsync(sessionOptions, cancellationToken: token), cancellationToken);
            return new CheckoutSession(session.Id, session.Url);
        }
        catch (BrokenCircuitException exception)
        {
            throw Unavailable(exception);
        }
        catch (Exception exception) when (exception is StripeException or HttpRequestException or TimeoutRejectedException)
        {
            throw new PaymentGatewayException("Stripe could not create the checkout session.", exception);
        }
    }

    public async Task<CheckoutSessionDetails?> GetCheckoutSessionAsync(
        string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var service = new SessionService(Client());
            var session = await _pipeline.ExecuteAsync(
                async token => await service.GetAsync(sessionId, cancellationToken: token), cancellationToken);
            return ToDetails(session);
        }
        catch (StripeException exception) when (exception.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (BrokenCircuitException exception)
        {
            throw Unavailable(exception);
        }
        catch (Exception exception) when (exception is StripeException or HttpRequestException or TimeoutRejectedException)
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
            return _client ??= _clientFactory(RequireSetting(_settings.SecretKey, "StripeSettings:SecretKey"));
        }
    }

    private PaymentGatewayUnavailableException Unavailable(BrokenCircuitException exception)
    {
        return new PaymentGatewayUnavailableException("Calls to Stripe are suspended after repeated failures.",
            _breakDuration, exception);
    }

    // An empty value, or the placeholder that older appsettings files carried, counts as not configured.
    private static string RequireSetting(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("change_me", StringComparison.OrdinalIgnoreCase))
            throw new PaymentGatewayNotConfiguredException($"{name} is not configured.");
        return value;
    }
}
