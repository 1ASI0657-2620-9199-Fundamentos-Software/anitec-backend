using System.Net.Mime;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Anitec.Platform.Notifications.Application;
using Anitec.Platform.Notifications.Application.CommandServices;
using Anitec.Platform.Notifications.Application.QueryServices;
using Anitec.Platform.Notifications.Domain.Model;
using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Model.Queries;
using Anitec.Platform.Notifications.Interfaces.Rest.Resources;
using Anitec.Platform.Shared.Application.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Notifications.Interfaces.Rest;

/// <summary>
///     The alerts of a rancher: due dates that nobody confirmed (US-070). Another rancher's alert answers 404, like
///     one that does not exist.
/// </summary>
[Authorize("Rancher")]
[ApiController]
[Route("api/v1/alerts")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Alerts about due dates that were not attended")]
public class AlertsController(
    IAlertCommandService commandService,
    IAlertQueryService queryService,
    TimeProvider clock,
    IOptions<NotificationsOptions> options) : ControllerBase
{
    private const int DefaultSnoozeDays = 3;

    /// <summary>The alerts of the authenticated rancher, the closest due date first.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var alerts = await queryService.Handle(new GetAlertsForCallerQuery(caller, status), cancellationToken);
        return Ok(alerts.Select(ToResource));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        var alert = await queryService.Handle(new GetAlertByIdForCallerQuery(id, caller), cancellationToken);
        return alert is null ? NotFound() : Ok(ToResource(alert));
    }

    /// <summary>The rancher attended the alert.</summary>
    [HttpPatch("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        return ToActionResult(await commandService.Handle(new ConfirmAlertCommand(id, caller), cancellationToken));
    }

    /// <summary>Postpones the alert. A reason is required and the new date is today plus the days that are asked.</summary>
    [HttpPatch("{id:int}/snooze")]
    public async Task<IActionResult> Snooze(int id, SnoozeAlertResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        return ToActionResult(await commandService.Handle(
            new SnoozeAlertCommand(id, caller, resource.Reason ?? string.Empty, resource.Days ?? DefaultSnoozeDays),
            cancellationToken));
    }

    /// <summary>Closes the alert without confirming the original attention, for example when the animal left the herd.</summary>
    [HttpPatch("{id:int}/close")]
    public async Task<IActionResult> Close(int id, CloseAlertResource resource, CancellationToken cancellationToken)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null) return Unauthorized();

        return ToActionResult(await commandService.Handle(new CloseAlertCommand(id, caller, resource.Reason),
            cancellationToken));
    }

    private IActionResult ToActionResult(Result<Alert> result)
    {
        if (result.IsSuccess) return Ok(ToResource(result.Value!));

        return result.Error switch
        {
            NotificationsError.AlertNotFound => NotFound(new { message = result.Message }),
            NotificationsError.ReasonRequired or NotificationsError.InvalidDays => BadRequest(new { message = result.Message }),
            _ => Conflict(new { message = result.Message })
        };
    }

    private AlertResource ToResource(Alert alert)
    {
        return new AlertResource(
            alert.Id,
            alert.SourceType,
            alert.SourceId,
            alert.Title,
            alert.AnimalId,
            alert.DueDate,
            alert.Status,
            alert.IsOverdue(options.Value.Today(clock)),
            alert.ResolutionReason,
            alert.ResolvedAt is null ? null : DateTime.SpecifyKind(alert.ResolvedAt.Value, DateTimeKind.Utc),
            DateTime.SpecifyKind(alert.CreatedAt, DateTimeKind.Utc),
            alert.NotifiedAt is not null);
    }
}
