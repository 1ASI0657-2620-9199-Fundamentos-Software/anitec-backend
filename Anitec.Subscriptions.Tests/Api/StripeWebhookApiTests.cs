using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Subscriptions.Tests.Support;

namespace Anitec.Subscriptions.Tests.Api;

/// <summary>The webhook: only a correctly signed event changes anything, and each session is stored once.</summary>
public class StripeWebhookApiTests : IClassFixture<SubscriptionsApiFactory>
{
    private readonly SubscriptionsApiFactory _factory;

    public StripeWebhookApiTests(SubscriptionsApiFactory factory)
    {
        _factory = factory;
        _factory.Gateway.NotConfigured = false;
        _factory.Gateway.Failing = false;
    }

    [Fact]
    public async Task ASignedPaidEvent_StoresTheSubscriptionAndThePayment_WithoutAUserToken()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 2);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("handled").GetBoolean());
        Assert.False(body.GetProperty("duplicate").GetBoolean());

        var subscription = Assert.Single(await _factory.SubscriptionsOfAsync(userId));
        Assert.Equal("Active", subscription.Status);
        Assert.Equal(2, subscription.PlanId);
        Assert.StartsWith("sub_test_", subscription.StripeSubscriptionId);
        var payment = Assert.Single(await _factory.PaymentsOfAsync(userId));
        Assert.Equal(99m, payment.Amount);
        Assert.Equal("PEN", payment.Currency);
        Assert.Equal("StripeTest", payment.Provider);
        Assert.Equal(sessionId, payment.ProviderPaymentId);
        Assert.Equal("Paid", payment.Status);
        Assert.Equal(subscription.Id, payment.SubscriptionId);
    }

    [Fact]
    public async Task TheSameEventTwice_IsStoredOnce()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 1);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var first = await _factory.PostWebhookAsync(payload, signature);
        var second = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicate").GetBoolean());
        Assert.Single(await _factory.PaymentsOfAsync(userId));
        Assert.Single(await _factory.SubscriptionsOfAsync(userId));
    }

    [Fact]
    public async Task AnEventWithAWrongSignature_Answers400_AndStoresNothing()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 2);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, _) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload, "forged-signature");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
        Assert.Empty(await _factory.SubscriptionsOfAsync(userId));
    }

    [Fact]
    public async Task AnEventWhoseBodyWasChangedAfterSigning_Answers400()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 2);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload.Replace("checkout.session.completed", "checkout.session.expired"), signature);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
    }

    [Fact]
    public async Task AnEventWithoutSignatureHeader_Answers400()
    {
        var sessionId = await OpenCheckoutAsync(SubscriptionsApiFactory.NewUserId(), planId: 1);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, _) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnEventOfAnotherType_IsAcknowledgedAndIgnored()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 1);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId, "invoice.paid");

        var response = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("handled").GetBoolean());
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
    }

    [Fact]
    public async Task ACompletedSessionThatWasNotPaid_IsIgnored()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 1);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("handled").GetBoolean());
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
    }

    [Fact]
    public async Task APaidSessionNotOpenedByTheService_IsAcknowledgedAndNotStored()
    {
        var sessionId = _factory.Gateway.AddForeignPaidSession();
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var response = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("handled").GetBoolean());
    }

    [Fact]
    public async Task WithoutTheWebhookSecret_Answers503()
    {
        _factory.Gateway.NotConfigured = true;

        var response = await _factory.PostWebhookAsync("{}", "anything");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [MySqlFact]
    public async Task EightWebhooksAtTheSameTime_StoreTheSessionOnce_OnTheRealDatabase()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(userId, planId: 2);
        _factory.Gateway.MarkPaid(sessionId);
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => _factory.PostWebhookAsync(payload, signature))));

        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.InternalServerError));
        // Stripe retries a 500, so a retry after the burst must leave exactly one payment and one subscription.
        var retry = await _factory.PostWebhookAsync(payload, signature);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single(await _factory.PaymentsOfAsync(userId));
        Assert.Single(await _factory.SubscriptionsOfAsync(userId));
    }

    private async Task<string> OpenCheckoutAsync(int userId, int planId)
    {
        var response = await _factory.CreateClientFor(userId)
            .PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
    }
}
