using System.Net.Mime;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Livestock.Application.CommandServices;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Commands;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Livestock.Interfaces.Rest.Resources;
using Anitec.Platform.Livestock.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Livestock.Interfaces.Rest;

[Authorize("Rancher", "Veterinarian")]
[ApiController]
[Route("api/v1/herds")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Herds endpoints")]
public class HerdsController(
    IHerdCommandService commandService,
    IHerdQueryService queryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetHerdsForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(HerdResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetHerdByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(HerdResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [Authorize("Rancher")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateHerdResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.OwnerId != caller.Id) return NotFound();

        if (string.IsNullOrWhiteSpace(resource.Name)) return BadRequest(new { message = "Herd name is required." });
        if (string.IsNullOrWhiteSpace(resource.Location)) return BadRequest(new { message = "Herd location is required." });
        if (string.IsNullOrWhiteSpace(resource.Owner)) return BadRequest(new { message = "Herd owner is required." });
        if (resource.OwnerId <= 0) return BadRequest(new { message = "OwnerId must be greater than zero." });
        if (string.IsNullOrWhiteSpace(resource.MainType)) return BadRequest(new { message = "Main type is required." });

        var command = CreateHerdCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new { message = result.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            HerdResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [Authorize("Rancher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateHerdResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.OwnerId != caller.Id
            || await queryService.Handle(new GetHerdByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var command = UpdateHerdCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(HerdResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [Authorize("Rancher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetHerdByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteHerdCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }
}
