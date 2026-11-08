using System.Security.Cryptography;
using System.Text;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Options;
using Stripe;

namespace Anitec.Subscriptions.Tests.Infrastructure;

/// <summary>
///     The real Stripe gateway, without network: signatures are produced with the documented scheme
///     (<c>t=timestamp,v1=HMAC-SHA256(secret, "timestamp.payload")</c>) using a made-up secret.
/// </summary>
public class StripePaymentGatewayTests
{
    private const string Secret = "whsec_unit_test_made_up_value";

    private static StripePaymentGateway CreateGateway(string? webhookSecret = Secret, string? secretKey = "sk_test_made_up")
    {
        return new StripePaymentGateway(Options.Create(new StripeSettings
        {
            SecretKey = secretKey,
            WebhookSecret = webhookSecret,
            SuccessUrl = "http://localhost/success?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = "http://localhost/cancel"
        }));
    }

    private static string EventJson(string status = "complete", string paymentStatus = "paid", string type = "checkout.session.completed")
    {
        return $$"""
                 {
                   "id": "evt_test_1",
                   "object": "event",
                   "api_version": "2020-01-01",
                   "created": 1700000000,
                   "livemode": false,
                   "pending_webhooks": 1,
                   "type": "{{type}}",
                   "data": {
                     "object": {
                       "id": "cs_test_abc",
                       "object": "checkout.session",
                       "status": "{{status}}",
                       "payment_status": "{{paymentStatus}}",
                       "customer": "cus_test_1",
                       "subscription": "sub_test_1",
                       "metadata": { "userId": "42", "planId": "2" }
                     }
                   }
                 }
                 """;
    }

    private static string Sign(string payload, string secret = Secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    [Fact]
    public void AValidSignedEvent_IsParsed_WithTheUserAndPlanOfTheMetadata()
    {
        var payload = EventJson();

        var result = CreateGateway().ParseWebhook(payload, Sign(payload));

        Assert.Equal("evt_test_1", result.Id);
        Assert.Equal("checkout.session.completed", result.Type);
        var session = Assert.IsType<CheckoutSessionDetails>(result.Session);
        Assert.Equal("cs_test_abc", session.Id);
        Assert.True(session.IsPaid);
        Assert.Equal(42, session.UserId);
        Assert.Equal(2, session.PlanId);
        Assert.Equal("cus_test_1", session.CustomerId);
        Assert.Equal("sub_test_1", session.SubscriptionId);
    }

    [Theory]
    [InlineData("open", "unpaid")]
    [InlineData("complete", "unpaid")]
    [InlineData("open", "paid")]
    public void ASessionIsPaidOnlyWhenItIsCompleteAndTheMoneyWasCollected(string status, string paymentStatus)
    {
        var payload = EventJson(status, paymentStatus);

        var result = CreateGateway().ParseWebhook(payload, Sign(payload));

        Assert.False(result.Session!.IsPaid);
    }

    [Fact]
    public void ASessionWithoutPaymentRequired_CountsAsPaid()
    {
        var payload = EventJson(paymentStatus: "no_payment_required");

        var result = CreateGateway().ParseWebhook(payload, Sign(payload));

        Assert.True(result.Session!.IsPaid);
    }

    [Fact]
    public void ASignatureMadeWithAnotherSecret_IsRejected()
    {
        var payload = EventJson();

        Assert.Throws<InvalidWebhookSignatureException>(() =>
            CreateGateway().ParseWebhook(payload, Sign(payload, "whsec_someone_else")));
    }

    [Fact]
    public void APayloadChangedAfterSigning_IsRejected()
    {
        var payload = EventJson();
        var signature = Sign(payload);

        Assert.Throws<InvalidWebhookSignatureException>(() =>
            CreateGateway().ParseWebhook(payload.Replace("\"planId\": \"2\"", "\"planId\": \"3\""), signature));
    }

    [Fact]
    public void AnOldSignature_IsRejected_ToStopReplays()
    {
        var payload = EventJson();

        Assert.Throws<InvalidWebhookSignatureException>(() =>
            CreateGateway().ParseWebhook(payload, Sign(payload, at: DateTimeOffset.UtcNow.AddHours(-1))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-signature")]
    public void AMissingOrMalformedHeader_IsRejected(string? header)
    {
        Assert.Throws<InvalidWebhookSignatureException>(() => CreateGateway().ParseWebhook(EventJson(), header));
    }

    [Fact]
    public void AMalformedBodyWithAValidSignature_IsRejected()
    {
        const string payload = "this is not json";

        Assert.Throws<InvalidWebhookSignatureException>(() => CreateGateway().ParseWebhook(payload, Sign(payload)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("whsec_change_me")]
    public void WithoutAWebhookSecret_TheGatewayIsNotConfigured(string? secret)
    {
        var payload = EventJson();

        Assert.Throws<PaymentGatewayNotConfiguredException>(() =>
            CreateGateway(webhookSecret: secret).ParseWebhook(payload, Sign(payload)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sk_test_change_me")]
    public async Task WithoutASecretKey_CreatingOrReadingASessionIsNotConfigured(string? key)
    {
        var gateway = CreateGateway(secretKey: key);

        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            gateway.CreateCheckoutSessionAsync(new CheckoutSessionRequest(1, 1, "Plan", 10, 49m, "PEN"), CancellationToken.None));
        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            gateway.GetCheckoutSessionAsync("cs_test_x", CancellationToken.None));
    }

    [Fact]
    public async Task TheGateway_NeverWritesTheKeyToTheStaticStripeConfiguration()
    {
        var previous = StripeConfiguration.ApiKey;
        var gateway = CreateGateway(secretKey: "sk_test_made_up_for_this_test");
        var payload = EventJson();

        gateway.ParseWebhook(payload, Sign(payload));
        // The call reaches a client with its own key and fails offline or is rejected; either way nothing is global.
        try
        {
            using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
            await gateway.GetCheckoutSessionAsync("cs_test_x", cancelled.Token);
        }
        catch (Exception)
        {
            // Not relevant to this test.
        }

        Assert.Equal(previous, StripeConfiguration.ApiKey);
    }
}
