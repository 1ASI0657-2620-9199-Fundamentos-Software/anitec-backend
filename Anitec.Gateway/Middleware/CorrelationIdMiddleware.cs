using System.Text.RegularExpressions;

namespace Anitec.Gateway.Middleware;

/// <summary>
///     Gives every request a correlation id. A well-formed <c>X-Correlation-Id</c> sent by the caller is kept, any
///     other value is replaced, so a client cannot inject odd text into the logs of the services. The id travels to
///     the service (YARP forwards the request headers) and comes back in the response.
/// </summary>
public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = incoming is not null && ValidId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Request.Headers[HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ValidId();
}
