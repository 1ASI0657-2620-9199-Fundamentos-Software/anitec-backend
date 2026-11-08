using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Anitec.Gateway.Tests.Support;

[CollectionDefinition(Name)]
public class GatewayCollection : ICollectionFixture<GatewayFixture>
{
    public const string Name = "gateway";
}

/// <summary>
///     The real gateway in memory, pointed at three fake services: two replicas of the core and Subscriptions.
///     The tests of this collection run one after another because they share the services and some of them stop one.
/// </summary>
public class GatewayFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public FakeService Api1 { get; } = new("api1");
    public FakeService Api2 { get; } = new("api2");
    public FakeService Subscriptions { get; } = new("subscriptions");

    /// <summary>The fixture of the running collection; the Gherkin steps use it because they cannot receive it by constructor.</summary>
    public static GatewayFixture Current { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        await Api1.StartAsync();
        await Api2.StartAsync();
        await Subscriptions.StartAsync();

        // Program reads part of its configuration before the host is built, so everything is set as environment variables.
        Set("ReverseProxy__Clusters__core__Destinations__api1__Address", Api1.Address);
        Set("ReverseProxy__Clusters__core__Destinations__api2__Address", Api2.Address);
        Set("ReverseProxy__Clusters__subscriptions__Destinations__subscriptions__Address", Subscriptions.Address);
        // Short intervals so the tests do not wait for the production values.
        foreach (var cluster in new[] { "core", "subscriptions" })
        {
            Set($"ReverseProxy__Clusters__{cluster}__HealthCheck__Active__Interval", "00:00:01");
            Set($"ReverseProxy__Clusters__{cluster}__HealthCheck__Active__Timeout", "00:00:01");
            Set($"ReverseProxy__Clusters__{cluster}__HealthCheck__Passive__ReactivationPeriod", "00:00:02");
        }

        Set("Gateway__ExposeStatus", "true");
        Set("Gateway__PassiveHealthWindowSeconds", "2");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        Client = _factory.CreateClient();
        Current = this;
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await Api1.DisposeAsync();
        await Api2.DisposeAsync();
        await Subscriptions.DisposeAsync();
    }

    /// <summary>Clears what the fakes recorded and makes sure the three of them are running and healthy.</summary>
    public async Task ResetAsync()
    {
        await Api1.StartAsync();
        await Api2.StartAsync();
        await Subscriptions.StartAsync();
        await WaitUntilAsync(async () =>
            (await StatusAsync()).All(item => item.Health == "Healthy"));
        Api1.Clear();
        Api2.Clear();
        Subscriptions.Clear();
    }

    public async Task<List<(string Cluster, string Destination, string Health)>> StatusAsync()
    {
        var json = await Client.GetFromJsonAsync<System.Text.Json.JsonElement>("/gateway/status");
        return json.GetProperty("clusters").EnumerateArray()
            .SelectMany(cluster => cluster.GetProperty("destinations").EnumerateArray().Select(destination => (
                cluster.GetProperty("cluster").GetString()!,
                destination.GetProperty("destination").GetString()!,
                destination.GetProperty("health").GetString()!)))
            .ToList();
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, int seconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("The condition was not met in time.");
    }

    private static void Set(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
    }
}
