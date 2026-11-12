using System.Net;
using Anitec.Gateway.Tests.Support;

namespace Anitec.Gateway.Tests.Api;

/// <summary>CORS decided once at the gateway, and the headers a balancer in front of it sets.</summary>
[Collection(GatewayCollection.Name)]
public class CorsAndHeadersTests(GatewayFixture fixture) : IAsyncLifetime
{
    private const string AllowedOrigin = "http://localhost:5173";

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
    public async Task APreflightFromAnAllowedFrontend_IsAnsweredByTheGateway()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/subscriptions/stripe-checkout");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Empty(fixture.Subscriptions.Requests);
    }

    [Fact]
    public async Task APreflightFromAnotherOrigin_GetsNoPermission()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/animals");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await fixture.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/api/v1/animals")]
    [InlineData("/api/v1/subscription-plans")]
    public async Task TheAllowOriginHeader_AppearsOnce_AndTheServiceNeverReceivesTheOrigin(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Origin", AllowedOrigin);

        var response = await fixture.Client.SendAsync(request);

        // The fake services add the header whenever they see an Origin, as the real ones do. Two values would make
        // the browser reject the answer, so the gateway removes the Origin before forwarding.
        Assert.Equal(AllowedOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.DoesNotContain(CoreRequests.Concat(fixture.Subscriptions.Requests), item => item.Headers.ContainsKey("Origin"));
        Assert.Contains("X-Correlation-Id", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
    }

    [Fact]
    public async Task ARequestFromAnotherOrigin_GetsNoAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/animals");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await fixture.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task TheHeadersOfTheBalancerInFront_AreTrustedAndPassedOn()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/animals");
        request.Headers.Add("X-Forwarded-For", "203.0.113.9");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "app.example.com");

        await fixture.Client.SendAsync(request);

        var received = CoreRequests.Single().Headers;
        Assert.Contains("203.0.113.9", received["X-Forwarded-For"]);
        Assert.Equal("https", received["X-Forwarded-Proto"]);
        Assert.Equal("app.example.com", received["X-Forwarded-Host"]);
    }
}
