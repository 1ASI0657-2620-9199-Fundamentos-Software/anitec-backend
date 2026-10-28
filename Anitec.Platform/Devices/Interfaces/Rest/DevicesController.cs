using System.Net.Mime;
using Anitec.Platform.Devices.Application.CommandServices;
using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Commands;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Devices.Interfaces.Rest.Resources;
using Anitec.Platform.Devices.Interfaces.Rest.Transform;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Infrastructure.Pipeline;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Metrics.Application.QueryServices;
using Anitec.Platform.Metrics.Domain.Model.Queries;
using Anitec.Platform.Metrics.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Devices.Interfaces.Rest;

[Authorize("Rancher", "Veterinarian")]
[ApiController]
[Route("api/v1/devices")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Devices endpoints")]
public class DevicesController(
    IDeviceCommandService commandService,
    IDeviceQueryService queryService,
    IDeviceMetricQueryService metricQueryService,
    IAnimalQueryService animalQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetDevicesForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(DeviceResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetDeviceByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(DeviceResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [Authorize("Rancher")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateDeviceResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.HerdId is null && resource.AnimalId is null)
            return BadRequest(new { message = "HerdId or AnimalId is required." });
        if (!await IsTargetOwnedByCallerAsync(resource.HerdId, resource.AnimalId, caller, cancellationToken))
            return NotFound();

        var command = CreateDeviceCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            DeviceResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [Authorize("Rancher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateDeviceResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.HerdId is null && resource.AnimalId is null)
            return BadRequest(new { message = "HerdId or AnimalId is required." });

        var device = await queryService.Handle(new GetDeviceByIdQuery(id), cancellationToken);
        if (device is null
            || !await IsTargetOwnedByCallerAsync(device.HerdId, device.AnimalId, caller, cancellationToken)
            || !await IsTargetOwnedByCallerAsync(resource.HerdId, resource.AnimalId, caller, cancellationToken))
            return NotFound();

        var command = UpdateDeviceCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(DeviceResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [Authorize("Rancher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var device = await queryService.Handle(new GetDeviceByIdQuery(id), cancellationToken);
        if (device is null
            || !await IsTargetOwnedByCallerAsync(device.HerdId, device.AnimalId, caller, cancellationToken))
            return NotFound();

        var result = await commandService.Handle(new DeleteDeviceCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }

    /// <summary>
    ///     Issues or rotates the API key that the physical device uses to send telemetry. The key is shown
    ///     once and only its hash is stored.
    /// </summary>
    [Authorize("Rancher")]
    [HttpPost("{id:int}/api-key")]
    public async Task<IActionResult> IssueApiKey(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var device = await queryService.Handle(new GetDeviceByIdQuery(id), cancellationToken);
        if (device is null
            || !await IsTargetOwnedByCallerAsync(device.HerdId, device.AnimalId, caller, cancellationToken))
            return NotFound();

        var result = await commandService.Handle(new IssueDeviceApiKeyCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();

        Response.Headers.CacheControl = "no-store";
        return Ok(new DeviceApiKeyResource(id, result.Value!, DeviceApiKeyFilter.HeaderName));
    }

    [HttpGet("{id:int}/metrics")]
    public async Task<IActionResult> GetMetricsByDevice(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var device = await queryService.Handle(new GetDeviceByIdForCallerQuery(id, caller), cancellationToken);
        if (device is null) return NotFound();

        var metrics = await metricQueryService.Handle(new GetDeviceMetricsForCallerQuery(caller), cancellationToken);
        return Ok(metrics
            .Where(metric => metric.DeviceId == id)
            .OrderByDescending(metric => metric.RecordedAt)
            .Select(DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}/latest-metric")]
    public async Task<IActionResult> GetLatestMetricByDevice(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var device = await queryService.Handle(new GetDeviceByIdForCallerQuery(id, caller), cancellationToken);
        if (device is null) return NotFound();

        var metrics = await metricQueryService.Handle(new GetDeviceMetricsForCallerQuery(caller), cancellationToken);
        var latest = metrics
            .Where(metric => metric.DeviceId == id)
            .OrderByDescending(metric => metric.RecordedAt)
            .FirstOrDefault();

        if (latest is null) return NotFound();

        return Ok(DeviceMetricResourceFromEntityAssembler.ToResourceFromEntity(latest));
    }

    /// <summary>
    ///     A device is installed on a herd or on an animal; the rancher must own that target to create,
    ///     change, delete or issue a key for the device.
    /// </summary>
    private async Task<bool> IsTargetOwnedByCallerAsync(int? herdId, int? animalId, CallerContext caller, CancellationToken cancellationToken)
    {
        if (herdId is { } herd)
            return await animalQueryService.Handle(new IsHerdOwnedByCallerQuery(herd, caller), cancellationToken);
        if (animalId is { } animal)
            return await animalQueryService.Handle(new AreAnimalsOwnedByCallerQuery([animal], caller), cancellationToken);
        return false;
    }
}
