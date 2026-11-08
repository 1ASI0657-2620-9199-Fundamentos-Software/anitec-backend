using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Anitec.Gateway.Middleware;

/// <summary>
///     When the gateway itself cannot reach a service (connection refused, timeout, or no healthy destination left)
///     it answers with a JSON problem that names the cluster and carries the correlation id, instead of an empty
///     502/503/504. Answers that come from the service, even a 503 of its own, are never replaced.
/// </summary>
public class UpstreamErrorMiddleware(RequestDelegate next, ILogger<UpstreamErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.HasStarted) return;

        var proxy = context.Features.Get<IReverseProxyFeature>();
        var forwarderError = context.Features.Get<IForwarderErrorFeature>();
        var noDestination = proxy is not null
                            && proxy.AvailableDestinations.Count == 0
                            && context.Response.StatusCode == StatusCodes.Status503ServiceUnavailable;
        if (forwarderError is null && !noDestination) return;

        var status = context.Response.StatusCode is >= 500 and <= 599
            ? context.Response.StatusCode
            : StatusCodes.Status502BadGateway;
        var cluster = proxy?.Cluster.Config.ClusterId ?? "unknown";
        logger.LogWarning("Upstream '{Cluster}' did not answer: {Reason}", cluster,
            forwarderError?.Error.ToString() ?? "no healthy destination");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = "Service unavailable",
            Detail = $"The '{cluster}' service did not answer. Other services keep working.",
            Extensions =
            {
                ["service"] = cluster,
                ["correlationId"] = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            }
        }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
