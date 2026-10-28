using System.Net.Mime;
using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Metrics.Application.CommandServices;
using Anitec.Platform.Metrics.Application.QueryServices;
using Anitec.Platform.Metrics.Domain.Model.Commands;
using Anitec.Platform.Metrics.Domain.Model.Queries;
using Anitec.Platform.Metrics.Interfaces.Rest.Resources;
using Anitec.Platform.Metrics.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Metrics.Interfaces.Rest;

[Authorize("Rancher", "Veterinarian")]
[ApiController]
[Route("api/v1/device-metrics")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available DeviceMetrics endpoints")]
public class DeviceMetricsController(
    IDeviceMetricCommandService commandService,
    IDeviceMetricQueryService queryService,
    IDeviceQueryService deviceQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetDeviceMetricsForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetDeviceMetricByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    // Manual readings are written by the rancher that owns the device. Devices send theirs through the
    // telemetry endpoint with their API key.
    [Authorize("Rancher")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateDeviceMetricResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await deviceQueryService.Handle(new GetDeviceByIdForCallerQuery(resource.DeviceId, caller), cancellationToken) is null)
            return NotFound();

        var command = CreateDeviceMetricCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [Authorize("Rancher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateDeviceMetricResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetDeviceMetricByIdForCallerQuery(id, caller), cancellationToken) is null
            || await deviceQueryService.Handle(new GetDeviceByIdForCallerQuery(resource.DeviceId, caller), cancellationToken) is null)
            return NotFound();

        var command = UpdateDeviceMetricCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [Authorize("Rancher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetDeviceMetricByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteDeviceMetricCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }
}
