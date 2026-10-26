using System.Net.Mime;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Infrastructure.Pipeline;
using Anitec.Platform.Devices.Interfaces.Rest.Resources;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Metrics.Application.CommandServices;
using Anitec.Platform.Metrics.Domain.Model.Commands;
using Anitec.Platform.Metrics.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Devices.Interfaces.Rest;

/// <summary>
///     Telemetry ingestion for physical devices (QAS-11). The device authenticates with its own API key and
///     not with the JWT of a user, and each device may send one reading per configured interval.
/// </summary>
[ApiController]
[Route("api/v1/devices/{deviceId:int}/telemetry")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Telemetry ingestion for devices")]
public class DeviceTelemetryController(IDeviceMetricCommandService metricCommandService) : ControllerBase
{
    // The user-token middleware skips this endpoint; the device is authenticated by DeviceApiKeyFilter instead.
    [AllowAnonymous]
    [ServiceFilter(typeof(DeviceApiKeyFilter))]
    [EnableRateLimiting("telemetry")]
    [HttpPost]
    public async Task<IActionResult> Ingest(int deviceId, TelemetryReadingResource resource, CancellationToken cancellationToken)
    {
        if (HttpContext.Items[DeviceApiKeyFilter.ItemKey] is not Device device || device.Id != deviceId)
            return Unauthorized();

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(resource.Type)) errors.Add("Type is required.");
        if (string.IsNullOrWhiteSpace(resource.Unit)) errors.Add("Unit is required.");
        if (errors.Count > 0) return BadRequest(new { message = "One or more fields are invalid.", errors });

        var command = new CreateDeviceMetricCommand(
            device.Id,
            resource.Type.Trim(),
            resource.Value,
            resource.Unit.Trim(),
            resource.RecordedAt ?? DateTime.UtcNow);

        var result = await metricCommandService.Handle(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new { message = result.Message });

        return StatusCode(StatusCodes.Status201Created, DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }
}
