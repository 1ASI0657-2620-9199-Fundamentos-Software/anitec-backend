using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Tests.Support;

namespace Anitec.Subscriptions.Tests.Api;

/// <summary>Opening a checkout and the confirm endpoint that backs up the webhook.</summary>
public class StripeCheckoutApiTests : IClassFixture<SubscriptionsApiFactory>
{
    private readonly SubscriptionsApiFactory _factory;

    public StripeCheckoutApiTests(SubscriptionsApiFactory factory)
    {
        _factory = factory;
        _factory.Gateway.NotConfigured = false;
        _factory.Gateway.Failing = false;
    }

    [Fact]
    public async Task Checkout_UsesTheAmountOfThePlanAndTheUserOfTheToken()
    {
        var userId = SubscriptionsApiFactory.NewUserId();

        var response = await _factory.CreateClientFor(userId).PostAsJsonAsync(
            "/api/v1/subscriptions/stripe-checkout", new { planId = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("cs_test_", body.GetProperty("sessionId").GetString());
        Assert.StartsWith("https://checkout.test/", body.GetProperty("checkoutUrl").GetString());

        var request = _factory.Gateway.Requests.Last(item => item.UserId == userId);
        Assert.Equal(3, request.PlanId);
        Assert.Equal(129m, request.Price);
        Assert.Equal("PEN", request.Currency);
    }

    [Fact]
    public async Task Checkout_OfAPlanThatDoesNotExist_Answers404()
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId()).PostAsJsonAsync(
            "/api/v1/subscriptions/stripe-checkout", new { planId = 999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_OfAnInactivePlan_Answers404()
    {
        var inactivePlanId = await _factory.WithDbAsync(async db =>
        {
            var plan = new SubscriptionPlan { Name = "Retired", Price = 10, StripePriceId = string.Empty, MaxAnimals = 1, IsActive = false };
            db.Set<SubscriptionPlan>().Add(plan);
            await db.SaveChangesAsync();
            return plan.Id;
        });

        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId()).PostAsJsonAsync(
            "/api/v1/subscriptions/stripe-checkout", new { planId = inactivePlanId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_WithoutStripeKeys_Answers503_AndDoesNotLeakDetails()
    {
        _factory.Gateway.NotConfigured = true;

        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId()).PostAsJsonAsync(
            "/api/v1/subscriptions/stripe-checkout", new { planId = 1 });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StripeSettings", text);
    }

    [Fact]
    public async Task Checkout_WhenStripeFails_Answers502()
    {
        _factory.Gateway.Failing = true;

        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId()).PostAsJsonAsync(
            "/api/v1/subscriptions/stripe-checkout", new { planId = 1 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_OfAPaidSession_StoresItOnce_AndAnotherConfirmAndTheWebhookReuseIt()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var client = _factory.CreateClientFor(userId);
        var sessionId = await OpenCheckoutAsync(client, planId: 2);
        _factory.Gateway.MarkPaid(sessionId);

        var first = await client.GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");
        var second = await client.GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");
        var (payload, signature) = _factory.Gateway.BuildWebhook(sessionId);
        var webhook = await _factory.PostWebhookAsync(payload, signature);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", body.GetProperty("subscription").GetProperty("status").GetString());
        Assert.Equal(99m, body.GetProperty("payment").GetProperty("amount").GetDecimal());
        Assert.Equal("StripeTest", body.GetProperty("payment").GetProperty("provider").GetString());
        Assert.True((await webhook.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicate").GetBoolean());
        Assert.Single(await _factory.PaymentsOfAsync(userId));
        Assert.Single(await _factory.SubscriptionsOfAsync(userId));
    }

    [Fact]
    public async Task Confirm_OfASessionThatIsNotPaid_Answers400_AndStoresNothing()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var client = _factory.CreateClientFor(userId);
        var sessionId = await OpenCheckoutAsync(client, planId: 1);

        var response = await client.GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
        Assert.Empty(await _factory.SubscriptionsOfAsync(userId));
    }

    [Fact]
    public async Task Confirm_OfTheSessionOfAnotherUser_Answers404_PaidOrStored()
    {
        var owner = SubscriptionsApiFactory.NewUserId();
        var intruder = SubscriptionsApiFactory.NewUserId();
        var sessionId = await OpenCheckoutAsync(_factory.CreateClientFor(owner), planId: 2);
        var intruderClient = _factory.CreateClientFor(intruder);

        _factory.Gateway.MarkPaid(sessionId);
        var beforeStored = await intruderClient.GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");
        await _factory.CreateClientFor(owner).GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");
        var afterStored = await intruderClient.GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");

        Assert.Equal(HttpStatusCode.NotFound, beforeStored.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterStored.StatusCode);
        Assert.Empty(await _factory.PaymentsOfAsync(intruder));
    }

    [Fact]
    public async Task Confirm_OfASessionThatDoesNotExist_Answers404()
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId())
            .GetAsync("/api/v1/subscriptions/stripe-checkout/cs_test_unknown/confirm");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_OfAForeignPaidSessionWithoutMetadata_IsNeverAttachedToTheCaller()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var sessionId = _factory.Gateway.AddForeignPaidSession();

        var response = await _factory.CreateClientFor(userId)
            .GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await _factory.PaymentsOfAsync(userId));
    }

    [Fact]
    public async Task ABetterPlan_ClosesThePreviousSubscription()
    {
        var userId = SubscriptionsApiFactory.NewUserId();
        var client = _factory.CreateClientFor(userId);

        await _factory.PayAndNotifyAsync(await OpenCheckoutAsync(client, planId: 1));
        await _factory.PayAndNotifyAsync(await OpenCheckoutAsync(client, planId: 3));

        var subscriptions = await _factory.SubscriptionsOfAsync(userId);
        Assert.Equal(2, subscriptions.Count);
        var active = Assert.Single(subscriptions, item => item.Status == "Active");
        Assert.Equal(3, active.PlanId);
        var closed = Assert.Single(subscriptions, item => item.Status == "Inactive");
        Assert.Equal(1, closed.PlanId);
        Assert.NotNull(closed.EndsAt);
        Assert.Equal(2, (await _factory.PaymentsOfAsync(userId)).Count);

        var activeResponse = await client.GetAsync($"/api/v1/subscriptions/users/{userId}/active");
        Assert.Equal(3, (await activeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("planId").GetInt32());
    }

    private static async Task<string> OpenCheckoutAsync(HttpClient client, int planId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
    }
}
