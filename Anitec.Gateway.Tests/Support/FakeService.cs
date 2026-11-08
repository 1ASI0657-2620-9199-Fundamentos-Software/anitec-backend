using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Anitec.Gateway.Tests.Support;

public record RecordedRequest(string Method, string PathAndQuery, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>
///     A real HTTP server on a loopback port that stands in for the core or for Subscriptions. It records what the
///     gateway forwards, answers /health, and can be stopped and started again on the same port to simulate a failure.
///     Like the real services, it adds an Access-Control-Allow-Origin header when the request carries an Origin.
/// </summary>
public sealed class FakeService : IAsyncDisposable
{
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();
    private WebApplication? _app;

    public FakeService(string name)
    {
        Name = name;
        Port = FreePort();
    }

    public string Name { get; }
    public int Port { get; }
    public string Address => $"http://127.0.0.1:{Port}";

    /// <summary>Requests received, without the health checks of the gateway.</summary>
    public IReadOnlyList<RecordedRequest> Requests => _requests.ToArray();

    public bool IsRunning => _app is not null;

    /// <summary>Makes /health answer 503 while the service keeps running, as a service that is up but not ready.</summary>
    public bool ReportUnhealthy { get; set; }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    public void Clear()
    {
        _requests.Clear();
    }

    public async Task StartAsync()
    {
        if (_app is not null) return;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(Address);
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapGet("/health", () => ReportUnhealthy
            ? Results.Text("Unhealthy", statusCode: 503)
            : Results.Text("Healthy"));
        app.Map("/{**path}", async (HttpContext context) =>
        {
            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);
            _requests.Enqueue(new RecordedRequest(context.Request.Method,
                context.Request.Path + context.Request.QueryString, headers, body));

            context.Response.Headers["X-Served-By"] = Name;
            if (headers.TryGetValue("Origin", out var origin))
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;

            // A service can answer 503 on its own (for example "payments unavailable"); the gateway must not replace it.
            if (context.Request.Path.Value!.EndsWith("/force-503", StringComparison.Ordinal))
                return Results.Json(new { title = "Payments unavailable", from = Name }, statusCode: 503);

            return Results.Json(new { service = Name, path = context.Request.Path.Value });
        });

        await app.StartAsync();
        _app = app;
    }

    public async Task StopAsync()
    {
        if (_app is null) return;
        var app = _app;
        _app = null;
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
