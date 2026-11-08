using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Subscriptions.Tests.Support;

namespace Anitec.Subscriptions.Tests.Api;

/// <summary>Who can call the service and what each caller can see (token validation, roles and QAS-08).</summary>
public class AuthorizationApiTests : IClassFixture<SubscriptionsApiFactory>
{
    private readonly SubscriptionsApiFactory _factory;

    public AuthorizationApiTests(SubscriptionsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Plans_WithoutToken_Answer401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/subscription-plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Plans_WithTokenSignedWithAnotherSecret_Answer401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TokenFactory.Create(1, "Rancher", "another-secret-0123456789-abcdefghijklmnopqrstuvwxyz"));

        var response = await client.GetAsync("/api/v1/subscription-plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Plans_WithExpiredToken_Answer401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TokenFactory.Create(1, "Rancher", lifetime: TimeSpan.FromMinutes(-5)));

        var response = await client.GetAsync("/api/v1/subscription-plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Plans_WithUnknownRole_Answer403()
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId(), "Admin")
            .GetAsync("/api/v1/subscription-plans");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("Rancher")]
    [InlineData("Veterinarian")]
    public async Task Plans_AreListedForBothRoles_WithThePricesOfTheMigration(string role)
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId(), role)
            .GetAsync("/api/v1/subscription-plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plans = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Other tests may add plans to a shared database, so only the three plans of the migration are checked.
        var priceByName = plans.EnumerateArray()
            .GroupBy(plan => plan.GetProperty("name").GetString()!)
            .ToDictionary(group => group.Key, group => group.First().GetProperty("price").GetDecimal());
        Assert.Equal(49m, priceByName["Ganadero Basico"]);
        Assert.Equal(99m, priceByName["Ganadero Pro"]);
        Assert.Equal(129m, priceByName["Veterinario Profesional"]);
    }

    [Fact]
    public async Task ActiveSubscriptionAndPayments_OfAnotherUser_Answer404()
    {
        var owner = SubscriptionsApiFactory.NewUserId();
        var intruder = SubscriptionsApiFactory.NewUserId();
        await PayForPlanAsync(owner, planId: 2);

        var intruderClient = _factory.CreateClientFor(intruder);

        Assert.Equal(HttpStatusCode.NotFound,
            (await intruderClient.GetAsync($"/api/v1/subscriptions/users/{owner}/active")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await intruderClient.GetAsync($"/api/v1/subscriptions/users/{owner}/payments")).StatusCode);
    }

    [Fact]
    public async Task ActiveSubscriptionAndPayments_OfTheCaller_AreReturned()
    {
        var owner = SubscriptionsApiFactory.NewUserId();
        await PayForPlanAsync(owner, planId: 2);
        var client = _factory.CreateClientFor(owner);

        var active = await client.GetAsync($"/api/v1/subscriptions/users/{owner}/active");
        var payments = await client.GetAsync($"/api/v1/subscriptions/users/{owner}/payments");

        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.Equal(2, (await active.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("planId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, payments.StatusCode);
        Assert.Single((await payments.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task SubscriptionById_OfAnotherUser_Answers404_AndTheListShowsOnlyTheCallers()
    {
        var owner = SubscriptionsApiFactory.NewUserId();
        var intruder = SubscriptionsApiFactory.NewUserId();
        await PayForPlanAsync(owner, planId: 1);
        var subscriptionId = (await _factory.SubscriptionsOfAsync(owner)).Single().Id;

        var intruderClient = _factory.CreateClientFor(intruder);
        var byId = await intruderClient.GetAsync($"/api/v1/subscriptions/{subscriptionId}");
        var list = await intruderClient.GetAsync("/api/v1/subscriptions");

        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Empty((await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK,
            (await _factory.CreateClientFor(owner).GetAsync($"/api/v1/subscriptions/{subscriptionId}")).StatusCode);
    }

    [Fact]
    public async Task AdministrationEndpoints_OutsideDevelopment_Answer404()
    {
        var client = _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId());
        var plan = JsonContent.Create(new { name = "x", price = 1, stripePriceId = "", maxAnimals = 1, isActive = true });

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/v1/subscription-plans", plan)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/v1/subscription-plans/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync("/api/v1/subscriptions/mock-checkout", JsonContent.Create(new { planId = 1 }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/v1/subscriptions/1")).StatusCode);
    }

    private async Task PayForPlanAsync(int userId, int planId)
    {
        var started = await _factory.CreateClientFor(userId)
            .PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId });
        var sessionId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        var webhook = await _factory.PayAndNotifyAsync(sessionId);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
    }
}

/// <summary>The endpoints that only exist while developing use the Development environment.</summary>
public class DevelopmentEndpointsApiTests : IClassFixture<DevelopmentEndpointsApiTests.DevelopmentFactory>
{
    private readonly DevelopmentFactory _factory;

    public DevelopmentEndpointsApiTests(DevelopmentFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MockCheckout_TakesTheBuyerFromTheTokenAndTheAmountFromThePlan()
    {
        var buyer = SubscriptionsApiFactory.NewUserId();
        var victim = SubscriptionsApiFactory.NewUserId();

        // The body tries to charge another user and a manipulated amount; both are ignored.
        var response = await _factory.CreateClientFor(buyer).PostAsJsonAsync(
            "/api/v1/subscriptions/mock-checkout", new { userId = victim, planId = 2, amount = 0.01m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(buyer, body.GetProperty("subscription").GetProperty("userId").GetInt32());
        Assert.Equal(99m, body.GetProperty("payment").GetProperty("amount").GetDecimal());
        Assert.Empty(await _factory.SubscriptionsOfAsync(victim));
    }

    [Fact]
    public async Task PlanAdministration_IsAvailableInDevelopment()
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId()).PostAsJsonAsync(
            "/api/v1/subscription-plans",
            new { name = "Demo", price = 10, stripePriceId = "", maxAnimals = 5, isActive = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    public class DevelopmentFactory : SubscriptionsApiFactory
    {
        public DevelopmentFactory() : base("Development")
        {
        }
    }
}
