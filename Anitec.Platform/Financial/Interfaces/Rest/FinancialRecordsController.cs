using System.Net.Mime;
using Anitec.Platform.Financial.Application.CommandServices;
using Anitec.Platform.Financial.Application.QueryServices;
using Anitec.Platform.Financial.Domain.Model.Commands;
using Anitec.Platform.Financial.Domain.Model.Queries;
using Anitec.Platform.Financial.Interfaces.Rest.Resources;
using Anitec.Platform.Financial.Interfaces.Rest.Transform;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Financial.Interfaces.Rest;


[Authorize("Rancher")]
[ApiController]
[Route("api/v1/financial-records")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available FinancialRecords endpoints")]
public class FinancialRecordsController(
    IFinancialRecordCommandService commandService,
    IFinancialRecordQueryService queryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetFinancialRecordsForCallerQuery(caller), cancellationToken);
        return Ok(result.Select(FinancialRecordResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetFinancialRecordByIdForCallerQuery(id, caller), cancellationToken);
        if (result is null) return NotFound();
        return Ok(FinancialRecordResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateFinancialRecordResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.OwnerId != caller.Id) return NotFound();

        var command = CreateFinancialRecordCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            FinancialRecordResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateFinancialRecordResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (resource.OwnerId != caller.Id
            || await queryService.Handle(new GetFinancialRecordByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var command = UpdateFinancialRecordCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(FinancialRecordResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();
        if (await queryService.Handle(new GetFinancialRecordByIdForCallerQuery(id, caller), cancellationToken) is null)
            return NotFound();

        var result = await commandService.Handle(new DeleteFinancialRecordCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }
}
