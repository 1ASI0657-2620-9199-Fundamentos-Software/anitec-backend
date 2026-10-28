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
[Route("api/v1/corrals")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Corrals endpoints")]
public class CorralsController(
    ICorralCommandService commandService,
    ICorralQueryService queryService,
    IHerdQueryService herdQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetCorralsForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(CorralResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetCorralByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(CorralResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [Authorize("Rancher")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateCorralResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (!await herdQueryService.Handle(new IsHerdOwnedByCallerQuery(resource.HerdId, caller), cancellationToken))
            return NotFound();

        if (string.IsNullOrWhiteSpace(resource.Name)) return BadRequest(new { message = "Corral name is required." });
        if (resource.HerdId <= 0) return BadRequest(new { message = "HerdId must be greater than zero." });

        var command = CreateCorralCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new { message = result.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            CorralResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [Authorize("Rancher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateCorralResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetCorralByIdForCallerQuery(id, caller), cancellationToken) is null
            || !await herdQueryService.Handle(new IsHerdOwnedByCallerQuery(resource.HerdId, caller), cancellationToken))
            return NotFound();

        var command = UpdateCorralCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(CorralResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [Authorize("Rancher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetCorralByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteCorralCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }
}
