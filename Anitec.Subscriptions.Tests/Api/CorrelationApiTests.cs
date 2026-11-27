using System.Net;
using System.Net.Http.Headers;
using Anitec.Subscriptions.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Anitec.Subscriptions.Tests.Api;

/// <summary>The id that the gateway creates reaches the logs of this service and comes back to the caller (QAS-10).</summary>
public class CorrelationApiTests : IClassFixture<SubscriptionsApiFactory>
{
    private readonly SubscriptionsApiFactory _factory;

    public CorrelationApiTests(SubscriptionsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TheIdSentByTheGateway_IsReturned_AndAppearsInTheLogLineOfTheRequest()
    {
        var capture = new LogCapture();
        using var client = ClientWith(capture);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/subscription-plans");
        request.Headers.Add("X-Correlation-Id", "gateway-req-0001");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("gateway-req-0001", response.Headers.GetValues("X-Correlation-Id").Single());
        var line = Assert.Single(capture.Lines,
            item => item.Message.Contains("/api/v1/subscription-plans responded 401"));
        Assert.Equal("gateway-req-0001", line.CorrelationId);
    }

    [Fact]
    public async Task WithoutAnId_OneIsCreated()
    {
        var capture = new LogCapture();
        using var client = ClientWith(capture);

        var response = await client.GetAsync("/api/v1/subscription-plans");

        var id = response.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.Contains(capture.Lines, item => item.CorrelationId == id);
    }

    [Fact]
    public async Task AMalformedId_IsReplaced()
    {
        var capture = new LogCapture();
        using var client = ClientWith(capture);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad value with spaces");

        var response = await client.SendAsync(request);

        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task TheLogLine_NeverContainsTheTokenOrTheQuery()
    {
        var capture = new LogCapture();
        using var client = ClientWith(capture);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "secret-token-value");

        await client.GetAsync("/api/v1/subscription-plans?apiKey=secret-query-value");

        Assert.DoesNotContain(capture.Lines, item =>
            item.Message.Contains("secret-token-value") || item.Message.Contains("secret-query-value"));
    }

    private HttpClient ClientWith(LogCapture capture)
    {
        return _factory.WithWebHostBuilder(builder =>
                builder.ConfigureLogging(logging => logging.AddProvider(capture)))
            .CreateClient();
    }
}
