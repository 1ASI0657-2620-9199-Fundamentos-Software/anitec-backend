using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Gateway.Tests.Support;

namespace Anitec.Gateway.Tests.Api;

/// <summary>
///     QAS-09: a service that fails does not drag down the others, and a failed replica of the core leaves the rotation.
///     These tests stop real servers and wait for the health checks, so they take a few seconds each.
/// </summary>
[Collection(GatewayCollection.Name)]
public class ResilienceTests(GatewayFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return fixture.ResetAsync();
    }

    [Fact]
    public async Task WhenSubscriptionsIsDown_ItsRoutesFailWithAClearError_AndTheCoreKeepsAnswering()
    {
        await fixture.Subscriptions.StopAsync();

        // Subscriptions: the first requests find the connection refused; later ones find no healthy destination.
        for (var i = 0; i < 4; i++)
        {
            var failed = await fixture.Client.GetAsync("/api/v1/subscription-plans");
            Assert.True(failed.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable,
                $"Unexpected status {failed.StatusCode}");
            Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
            var body = await failed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("subscriptions", body.GetProperty("service").GetString());
            Assert.Equal(failed.Headers.GetValues("X-Correlation-Id").Single(), body.GetProperty("correlationId").GetString());
        }

        // The rest of the system: no degradation at all.
        var results = new List<HttpStatusCode>();
        for (var i = 0; i < 20; i++)
            results.Add((await fixture.Client.GetAsync("/api/v1/animals")).StatusCode);
        Assert.All(results, status => Assert.Equal(HttpStatusCode.OK, status));
        Assert.Equal(20, fixture.Api1.Requests.Count + fixture.Api2.Requests.Count);
    }

    [Fact]
    public async Task WhenSubscriptionsComesBack_ItsRoutesWorkAgain()
    {
        await fixture.Subscriptions.StopAsync();
        await fixture.Client.GetAsync("/api/v1/subscription-plans");
        await fixture.Subscriptions.StartAsync();

        await GatewayFixture.WaitUntilAsync(async () =>
            (await fixture.Client.GetAsync("/api/v1/subscription-plans")).StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenOneReplicaOfTheCoreFails_ItLeavesTheRotation_AndEveryRequestIsAnswered()
    {
        await fixture.Api1.StopAsync();

        // The first request to the dead replica may fail; after that it is out of the rotation.
        var failures = 0;
        for (var i = 0; i < 6; i++)
            if ((await fixture.Client.GetAsync("/api/v1/animals")).StatusCode != HttpStatusCode.OK)
                failures++;
        Assert.True(failures <= 1, $"{failures} requests failed before the replica left the rotation");

        fixture.Api2.Clear();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/animals")).StatusCode);
        Assert.Equal(10, fixture.Api2.Requests.Count);

        var status = await fixture.StatusAsync();
        Assert.Contains(("core", "api1", "Unhealthy"), status);
        Assert.Contains(("core", "api2", "Healthy"), status);
    }

    [Fact]
    public async Task WhenTheReplicaComesBack_ItReturnsToTheRotation()
    {
        await fixture.Api1.StopAsync();
        for (var i = 0; i < 4; i++) await fixture.Client.GetAsync("/api/v1/animals");
        await fixture.Api1.StartAsync();

        await GatewayFixture.WaitUntilAsync(async () =>
            (await fixture.StatusAsync()).Contains(("core", "api1", "Healthy")));
        fixture.Api1.Clear();
        fixture.Api2.Clear();

        // The list of destinations is adjusted when the replica returns, so the share is not exact at first:
        // what matters is that it receives traffic again and no request fails.
        await GatewayFixture.WaitUntilAsync(async () =>
        {
            Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/v1/animals")).StatusCode);
            return fixture.Api1.Requests.Count > 0 && fixture.Api2.Requests.Count > 0;
        });
    }
}
