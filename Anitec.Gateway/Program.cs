using Anitec.Gateway;
using Anitec.Gateway.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<GatewayOptions>(builder.Configuration.GetSection("Gateway"));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

// The gateway sits behind a balancer (nginx locally, the ingress of the cloud) that sets X-Forwarded-*. It runs in a
// private network, so the headers are trusted from any address instead of from a fixed proxy list.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// CORS is decided here, once. The routes remove the Origin header before forwarding, so the services do not add
// a second Access-Control-Allow-Origin that would make the browser reject the response.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedFrontends",
        policy => policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
});

// Passive health: one transport failure is enough to take a replica out of the rotation. How long it stays out is
// the greater of the ReactivationPeriod of the cluster (60 s when the file does not set it) and the detection window
// of the TransportFailureRate policy of YARP 2.3.0, as observed in the tests. The file sets the first one and this
// setting the second one, both to 10 s. The active check also has to see the replica healthy before it returns.
var passiveWindowSeconds = builder.Configuration.GetValue<int?>("Gateway:PassiveHealthWindowSeconds") ?? 10;
builder.Services.Configure<TransportFailureRateHealthPolicyOptions>(options =>
{
    options.MinimalTotalCountThreshold = 1;
    options.DefaultFailureRateLimit = 0.5;
    options.DetectionWindowSize = TimeSpan.FromSeconds(passiveWindowSeconds);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseCors("AllowedFrontends");
app.UseMiddleware<ApiVersionMiddleware>();
app.UseMiddleware<UpstreamErrorMiddleware>();

// The identity travels in the Authorization header, which the proxy forwards untouched. The gateway does not
// validate the token: each service validates it with the shared secret.
app.MapHealthChecks("/health");

if (app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value.ExposeStatus)
    app.MapGet("/gateway/status", (Yarp.ReverseProxy.IProxyStateLookup lookup) => Results.Ok(new
    {
        clusters = lookup.GetClusters().Select(cluster => new
        {
            cluster = cluster.ClusterId,
            destinations = cluster.Destinations.Select(pair => new
            {
                destination = pair.Key,
                health = EffectiveHealth(pair.Value.Health),
                active = pair.Value.Health.Active.ToString(),
                passive = pair.Value.Health.Passive.ToString()
            })
        })
    }));

app.MapReverseProxy();
app.Run();

// A destination receives traffic unless one of the checks marked it unhealthy.
static string EffectiveHealth(DestinationHealthState health)
{
    return health.Active == DestinationHealth.Unhealthy || health.Passive == DestinationHealth.Unhealthy
        ? nameof(DestinationHealth.Unhealthy)
        : nameof(DestinationHealth.Healthy);
}

// Makes the entry point visible to the integration tests.
public partial class Program;
