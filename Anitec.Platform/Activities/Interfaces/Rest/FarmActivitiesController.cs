using System.Net.Mime;
using Anitec.Platform.Activities.Application.CommandServices;
using Anitec.Platform.Activities.Application.QueryServices;
using Anitec.Platform.Activities.Domain.Model.Commands;
using Anitec.Platform.Activities.Domain.Model.Queries;
using Anitec.Platform.Activities.Interfaces.Rest.Resources;
using Anitec.Platform.Activities.Interfaces.Rest.Transform;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Shared.Domain.Model;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Activities.Interfaces.Rest;

[Authorize("Rancher", "Veterinarian")]
[ApiController]
[Route("api/v1/farm-events")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available FarmActivities endpoints")]
public class FarmActivitiesController(
    IFarmActivityCommandService commandService,
    IFarmActivityQueryService queryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetFarmActivitiesForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(FarmActivityResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetFarmActivityByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(FarmActivityResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFarmActivityResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (!IsOwnActivity(resource, caller)) return NotFound();

        var command = CreateFarmActivityCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            FarmActivityResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateFarmActivityResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (!IsOwnActivity(resource, caller)
            || await queryService.Handle(new GetFarmActivityByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var command = UpdateFarmActivityCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(FarmActivityResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetFarmActivityByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteFarmActivityCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }

    /// <summary>
    ///     A rancher can only create activities for itself; a veterinarian only activities assigned to itself.
    /// </summary>
    private static bool IsOwnActivity(CreateFarmActivityResource resource, CallerContext caller)
    {
        if (caller.IsRancher) return resource.OwnerId == caller.Id;
        return caller.IsVeterinarian && resource.VeterinarianId == caller.Id;
    }
}
