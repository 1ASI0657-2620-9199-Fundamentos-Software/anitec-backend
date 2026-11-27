using System.Net.Mime;
using Anitec.Platform.Iam.Application.QueryServices;
using Anitec.Platform.Iam.Domain.Model.Queries;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Shared.Application.AuditLogging;
using Anitec.Platform.Shared.Interfaces.Rest.Resources;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Shared.Interfaces.Rest;

/// <summary>
///     Lets a rancher see who did what with its data (US-074). The log is read-only: this controller has no
///     operation that creates, changes or removes an entry, and the rows come only from the sensitive operations of
///     the other endpoints.
/// </summary>
[Authorize("Rancher")]
[ApiController]
[Route("api/v1/audit-log")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Audit log of the sensitive operations on the data of the rancher")]
public class AuditLogController(
    IAuditLogQueryService queryService,
    IUserQueryService userQueryService) : ControllerBase
{
    /// <summary>
    ///     Entries of the authenticated rancher, newest first. The owner is always the caller, so there is no way to
    ///     ask for the entries of another rancher.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = AuditLogQueryService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var entries = await queryService.Handle(
            new GetAuditLogForOwnerQuery(caller.Id, action, entityType, from, to, limit), cancellationToken);

        var names = new Dictionary<int, string?>();
        foreach (var actorId in entries.Select(entry => entry.ActorUserId).Distinct())
            names[actorId] = (await userQueryService.Handle(new GetUserByIdQuery(actorId), cancellationToken))?.FullName;

        return Ok(entries.Select(entry => new AuditLogEntryResource(
            entry.Id,
            entry.CorrelationId,
            entry.ActorUserId,
            names.GetValueOrDefault(entry.ActorUserId),
            entry.ActorRole,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            DateTime.SpecifyKind(entry.OccurredAt, DateTimeKind.Utc),
            entry.Result,
            entry.DetailsJson)));
    }
}
