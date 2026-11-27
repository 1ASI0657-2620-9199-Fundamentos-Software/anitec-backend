using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Subscriptions.Tests.Support;

namespace Anitec.Subscriptions.Tests.Api;

/// <summary>The plan of the caller and its limit of animals, which the core asks for before it registers animals.</summary>
public class ActivePlanApiTests : IClassFixture<SubscriptionsApiFactory>
{
    private readonly SubscriptionsApiFactory _factory;

    public ActivePlanApiTests(SubscriptionsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WithoutToken_Answers401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/subscriptions/active-plan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WithoutAnActiveSubscription_Answers404()
    {
        var response = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId())
            .GetAsync("/api/v1/subscriptions/active-plan");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AfterPaying_ReturnsThePlanAndItsLimitOfAnimals()
    {
        var client = _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId());
        var checkout = await client.PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId = 2 });
        var sessionId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _factory.PayAndNotifyAsync(sessionId)).StatusCode);

        var response = await client.GetAsync("/api/v1/subscriptions/active-plan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("planId").GetInt32());
        Assert.Equal("Ganadero Pro", body.GetProperty("planName").GetString());
        Assert.Equal(250, body.GetProperty("maxAnimals").GetInt32());
        Assert.Equal("Active", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task EachUserSeesOnlyItsOwnPlan()
    {
        var owner = _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId());
        var checkout = await owner.PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId = 3 });
        var sessionId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        await _factory.PayAndNotifyAsync(sessionId);

        var other = await _factory.CreateClientFor(SubscriptionsApiFactory.NewUserId())
            .GetAsync("/api/v1/subscriptions/active-plan");

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
    }
}
