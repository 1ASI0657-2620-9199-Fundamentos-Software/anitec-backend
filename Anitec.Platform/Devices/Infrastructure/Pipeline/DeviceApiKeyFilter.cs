using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Anitec.Platform.Devices.Infrastructure.Pipeline;

/// <summary>
///     Authenticates a physical device with the <c>X-Device-Api-Key</c> header instead of the JWT of a user.
///     The device id comes from the route and the key must match the one issued for that device, so a device
///     can only report readings for itself. On success the device is stored in
///     <c>HttpContext.Items["Device"]</c>.
/// </summary>
/// <remarks>
///     AniTec authorizes users with its own middleware and attribute instead of the ASP.NET Core
///     authentication handlers, so the device credential follows the same style: an authorization filter
///     registered through <c>[ServiceFilter(typeof(DeviceApiKeyFilter))]</c>.
/// </remarks>
public class DeviceApiKeyFilter(IDeviceQueryService deviceQueryService) : IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Device-Api-Key";
    public const string ItemKey = "Device";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var headerValue = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue)
            || !context.RouteData.Values.TryGetValue("deviceId", out var routeValue)
            || !int.TryParse(routeValue?.ToString(), out var deviceId))
        {
            context.Result = Unauthorized();
            return;
        }

        var device = await deviceQueryService.Handle(
            new AuthenticateDeviceQuery(deviceId, headerValue.Trim()),
            context.HttpContext.RequestAborted);

        if (device is null)
        {
            context.Result = Unauthorized();
            return;
        }

        context.HttpContext.Items[ItemKey] = device;
    }

    private static IActionResult Unauthorized()
    {
        return new UnauthorizedObjectResult(new { message = "A valid device API key is required." });
    }
}
