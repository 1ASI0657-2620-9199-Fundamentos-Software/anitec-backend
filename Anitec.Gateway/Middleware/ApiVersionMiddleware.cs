using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Anitec.Gateway.Middleware;

/// <summary>
///     Rejects a request for an API version the gateway does not support, with a documented error, instead of
///     routing it silently to a service that would answer something else (TS-025). Only paths of the form
///     <c>/api/v{n}/...</c> are checked.
/// </summary>
public partial class ApiVersionMiddleware(RequestDelegate next, IOptions<GatewayOptions> options)
{
    // The configuration binder appends to an array that already has items, so the default lives here and not in
    // the options class (a default of ["v1"] would have produced "v1, v1" when the file also says v1).
    private readonly string[] _supported = options.Value.SupportedApiVersions is { Length: > 0 } configured
        ? configured
        : ["v1"];

    public async Task InvokeAsync(HttpContext context)
    {
        var match = VersionInPath().Match(context.Request.Path.Value ?? string.Empty);
        if (match.Success && !_supported.Contains(match.Groups["version"].Value, StringComparer.OrdinalIgnoreCase))
        {
            var requested = match.Groups["version"].Value;
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.Headers["api-supported-versions"] = string.Join(", ", _supported);
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unsupported API version",
                Detail = $"API version '{requested}' is not supported. Supported versions: {string.Join(", ", _supported)}.",
                Extensions =
                {
                    ["requestedVersion"] = requested,
                    ["supportedVersions"] = _supported,
                    ["correlationId"] = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
                }
            }, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
            return;
        }

        await next(context);
    }

    [GeneratedRegex(@"^/api/(?<version>v\d+)(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionInPath();
}
