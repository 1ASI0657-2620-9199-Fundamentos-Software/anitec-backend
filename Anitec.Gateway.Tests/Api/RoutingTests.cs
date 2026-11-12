using System.Net;
using System.Text;
using Anitec.Gateway.Tests.Support;

namespace Anitec.Gateway.Tests.Api;

/// <summary>Which service answers each path, and what the gateway keeps of the request.</summary>
[Collection(GatewayCollection.Name)]
public class RoutingTests(GatewayFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private IEnumerable<RecordedRequest> CoreRequests => fixture.Api1.Requests.Concat(fixture.Api2.Requests);

    [Fact]
    public async Task Health_IsAnsweredByTheGatewayItself()
    {
        var response = await fixture.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(CoreRequests);
        Assert.Empty(fixture.Subscriptions.Requests);
    }

    [Theory]
    [InlineData("/api/v1/animals")]
    [InlineData("/api/v1/herds/3/animals?page=2")]
    [InlineData("/api/v1/authentication/sign-in")]
    [InlineData("/uploads/animals/cow.png")]
    [InlineData("/swagger/index.html")]
    [InlineData("/api/v1/payments/1")]
    public async Task EverythingElse_GoesToTheCore(string path)
    {
        var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(CoreRequests);
        Assert.Equal(path, CoreRequests.Single().PathAndQuery);
        Assert.Empty(fixture.Subscriptions.Requests);
    }

    [Theory]
    [InlineData("/api/v1/subscription-plans")]
    [InlineData("/api/v1/subscription-plans/2")]
    [InlineData("/api/v1/subscriptions")]
    [InlineData("/api/v1/subscriptions/users/7/active")]
    [InlineData("/api/v1/subscriptions/users/7/payments")]
    [InlineData("/api/v1/subscriptions/stripe-checkout/cs_test_1/confirm")]
    public async Task SubscriptionPaths_GoToTheSubscriptionsService(string path)
    {
        var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("subscriptions", response.Headers.GetValues("X-Served-By").Single());
        Assert.Single(fixture.Subscriptions.Requests);
        Assert.Equal(path, fixture.Subscriptions.Requests.Single().PathAndQuery);
        Assert.Empty(CoreRequests);
    }

    [Fact]
    public async Task TheStripeWebhook_ReachesSubscriptions_WithItsBodyAndSignatureUntouched()
    {
        const string body = "{\"id\":\"evt_1\",\"type\":\"checkout.session.completed\",\"note\":\"ñ ü\"}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/stripe/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "t=1700000000,v1=abc123");

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var received = Assert.Single(fixture.Subscriptions.Requests);
        Assert.Equal("POST", received.Method);
        Assert.Equal(body, received.Body);
        Assert.Equal("t=1700000000,v1=abc123", received.Headers["Stripe-Signature"]);
    }

    [Fact]
    public async Task TheAuthorizationHeader_IsForwardedUntouched_AndNotValidatedByTheGateway()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/subscriptions/users/7/active");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer this-is-not-a-valid-token");

        var response = await fixture.Client.SendAsync(request);

        // The decision belongs to the service: the gateway neither rejects nor rewrites the token.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer this-is-not-a-valid-token",
            Assert.Single(fixture.Subscriptions.Requests).Headers["Authorization"]);
    }

    [Fact]
    public async Task ARequestWithoutToken_IsAlsoForwarded_SoTheServiceAnswers401ByItself()
    {
        await fixture.Client.GetAsync("/api/v1/animals");

        Assert.False(CoreRequests.Single().Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task ARequestOfTheCore_IsSharedBetweenTheTwoReplicas_RoundRobin()
    {
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/animals")).StatusCode);

        Assert.Equal(5, fixture.Api1.Requests.Count);
        Assert.Equal(5, fixture.Api2.Requests.Count);
    }

    [Fact]
    public async Task TheMethodsAndTheBodyOfThePost_ArePreserved()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/animals/4")
        {
            Content = new StringContent("{\"name\":\"Luna\"}", Encoding.UTF8, "application/json")
        };

        await fixture.Client.SendAsync(request);

        var received = Assert.Single(CoreRequests);
        Assert.Equal("PUT", received.Method);
        Assert.Equal("{\"name\":\"Luna\"}", received.Body);
    }
}
