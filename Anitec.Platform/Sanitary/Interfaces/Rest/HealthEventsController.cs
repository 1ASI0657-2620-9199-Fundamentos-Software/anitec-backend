using System.Net.Mime;
using Anitec.Platform.Sanitary.Application.CommandServices;
using Anitec.Platform.Sanitary.Application.QueryServices;
using Anitec.Platform.Sanitary.Domain.Model.Commands;
using Anitec.Platform.Sanitary.Domain.Model.Queries;
using Anitec.Platform.Sanitary.Interfaces.Rest.Resources;
using Anitec.Platform.Sanitary.Interfaces.Rest.Transform;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Sanitary.Interfaces.Rest;

[Authorize("Rancher", "Veterinarian")]
[ApiController]
[Route("api/v1/health-events")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available HealthEvents endpoints")]
public class HealthEventsController(
    IHealthEventCommandService commandService,
    IHealthEventQueryService queryService,
    IAnimalQueryService animalQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetHealthEventsForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(HealthEventResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetHealthEventByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(HealthEventResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateHealthEventResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (!await CanAccessAnimalAsync(resource.AnimalId, caller, cancellationToken)) return NotFound();

        var command = CreateHealthEventCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            HealthEventResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateHealthEventResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetHealthEventByIdForCallerQuery(id, caller), cancellationToken) is null
            || !await CanAccessAnimalAsync(resource.AnimalId, caller, cancellationToken))
            return NotFound();

        var command = UpdateHealthEventCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(HealthEventResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetHealthEventByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteHealthEventCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }

    private async Task<bool> CanAccessAnimalAsync(int animalId, CallerContext caller, CancellationToken cancellationToken)
    {
        var animal = await animalQueryService.Handle(new GetAnimalByIdForCallerQuery(animalId, caller), cancellationToken);
        return animal is not null;
    }
}
