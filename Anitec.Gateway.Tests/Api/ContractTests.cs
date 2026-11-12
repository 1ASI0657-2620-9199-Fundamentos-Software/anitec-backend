using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Gateway.Tests.Support;

namespace Anitec.Gateway.Tests.Api;

/// <summary>Correlation of requests and the documented error for an API version that is not supported (TS-025).</summary>
[Collection(GatewayCollection.Name)]
public class ContractTests(GatewayFixture fixture) : IAsyncLifetime
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
    public async Task WithoutACorrelationId_OneIsCreated_ReachesTheServiceAndComesBack()
    {
        var response = await fixture.Client.GetAsync("/api/v1/animals");

        var returned = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Matches("^[0-9a-f]{32}$", returned);
        Assert.Equal(returned, CoreRequests.Single().Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task AWellFormedCorrelationId_IsKept_ForCoreAndSubscriptions()
    {
        foreach (var path in new[] { "/api/v1/animals", "/api/v1/subscription-plans" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Correlation-Id", "mobile-app.2026-11-07_0042");

            var response = await fixture.Client.SendAsync(request);

            Assert.Equal("mobile-app.2026-11-07_0042", response.Headers.GetValues("X-Correlation-Id").Single());
        }

        Assert.Equal("mobile-app.2026-11-07_0042", CoreRequests.Single().Headers["X-Correlation-Id"]);
        Assert.Equal("mobile-app.2026-11-07_0042", fixture.Subscriptions.Requests.Single().Headers["X-Correlation-Id"]);
    }

    [Theory]
    [InlineData("has spaces in it")]
    [InlineData("semi;colon")]
    [InlineData("<script>alert(1)</script>")]
    public async Task AMalformedCorrelationId_IsReplaced(string sent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/animals");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", sent);

        var response = await fixture.Client.SendAsync(request);

        var returned = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual(sent, returned);
        Assert.Matches("^[0-9a-f]{32}$", returned);
    }

    [Fact]
    public async Task ATooLongCorrelationId_IsReplaced()
    {
        var sent = new string('a', 65);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/animals");
        request.Headers.Add("X-Correlation-Id", sent);

        var response = await fixture.Client.SendAsync(request);

        Assert.NotEqual(sent, response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Theory]
    [InlineData("/api/v2/subscriptions", "v2")]
    [InlineData("/api/v2/animals", "v2")]
    [InlineData("/api/v10/subscription-plans", "v10")]
    [InlineData("/api/v0/anything", "v0")]
    public async Task AnUnsupportedVersion_AnswersADocumentedError_AndIsNotRoutedToAnyService(string path, string version)
    {
        var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("v1", response.Headers.GetValues("api-supported-versions").Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unsupported API version", body.GetProperty("title").GetString());
        Assert.Equal(version, body.GetProperty("requestedVersion").GetString());
        Assert.Equal("v1", body.GetProperty("supportedVersions")[0].GetString());
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), body.GetProperty("correlationId").GetString());

        // Nothing was forwarded, not even to the core.
        Assert.Empty(CoreRequests);
        Assert.Empty(fixture.Subscriptions.Requests);
    }

    [Theory]
    [InlineData("/api/v1/animals")]
    [InlineData("/api/V1/animals")]
    [InlineData("/api/v1")]
    [InlineData("/api/versions")]
    [InlineData("/api/values/1")]
    public async Task ASupportedVersion_OrAPathThatIsNotVersioned_IsRouted(string path)
    {
        var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(CoreRequests);
    }

    [Fact]
    public async Task TheOwnAnswerOfAService_EvenA503_IsNotReplacedByTheGateway()
    {
        var response = await fixture.Client.GetAsync("/api/v1/subscriptions/force-503");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Payments unavailable", body.GetProperty("title").GetString());
        Assert.Equal("subscriptions", body.GetProperty("from").GetString());
    }
}
