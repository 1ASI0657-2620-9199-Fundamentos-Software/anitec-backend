using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Components;

/// <summary>
///     Makes the correlation id of the request visible in this service (QAS-10). The API gateway creates the id and
///     sends it in <c>X-Correlation-Id</c>; this middleware keeps a well-formed value, replaces any other one (so a
///     caller cannot inject odd text into the logs), puts it in the logging scope of the request, returns it in the
///     response and writes one line when the request ends. The line never contains headers, the query string or the
///     body, so tokens and secrets cannot reach the logs through it.
/// </summary>
public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Key under which the id is stored in <see cref="HttpContext.Items" /> for the code of the request.</summary>
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = incoming is not null && ValidId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Request.Headers[HeaderName] = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                await next(context);
            }
            finally
            {
                // The health checks of the gateway arrive every few seconds: they would bury the useful lines.
                var level = context.Request.Path == "/health" ? LogLevel.Debug : LogLevel.Information;
                logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
                    (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ValidId();
}
