using System.Net.Mime;
using Anitec.Platform.Clients.Application.CommandServices;
using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model;
using Anitec.Platform.Clients.Domain.Model.Commands;
using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Clients.Interfaces.Rest.Resources;
using Anitec.Platform.Iam.Application.QueryServices;
using Anitec.Platform.Iam.Domain.Model.Queries;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Platform.Clients.Interfaces.Rest;

/// <summary>
///     Lets a rancher review, approve, reject or revoke the access requests of veterinarians (US-023, US-071, US-072).
/// </summary>
[Authorize("Rancher")]
[ApiController]
[Route("api/v1/rancher/{rancherId:int}/veterinarian-requests")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Veterinarian access requests of a rancher")]
public class RancherVeterinarianRequestsController(
    IVeterinarianClientCommandService commandService,
    IVeterinarianClientQueryService queryService,
    IUserQueryService userQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRequests(int rancherId, CancellationToken cancellationToken)
    {
        if (!IsCallerRancher(rancherId, out var denied)) return denied!;

        var relationships = await queryService.Handle(new GetVeterinarianRequestsByRancherIdQuery(rancherId), cancellationToken);
        var users = (await userQueryService.Handle(new GetAllUsersQuery(), cancellationToken)).ToList();

        return Ok(relationships
            .OrderByDescending(relationship => relationship.RequestedAt)
            .Select(relationship => ToResource(relationship, users.FirstOrDefault(user => user.Id == relationship.VeterinarianId)?.FullName)));
    }

    [HttpPatch("{veterinarianId:int}/approve")]
    public async Task<IActionResult> Approve(int rancherId, int veterinarianId, CancellationToken cancellationToken)
    {
        if (!IsCallerRancher(rancherId, out var denied)) return denied!;

        var result = await commandService.Handle(new ApproveVeterinarianClientCommand(veterinarianId, rancherId), cancellationToken);
        return await ToActionResultAsync(result.IsSuccess, result.Error, result.Message, result.Value, cancellationToken);
    }

    [HttpPatch("{veterinarianId:int}/reject")]
    public async Task<IActionResult> Reject(int rancherId, int veterinarianId, CancellationToken cancellationToken)
    {
        if (!IsCallerRancher(rancherId, out var denied)) return denied!;

        var result = await commandService.Handle(new RejectVeterinarianClientCommand(veterinarianId, rancherId), cancellationToken);
        return await ToActionResultAsync(result.IsSuccess, result.Error, result.Message, result.Value, cancellationToken);
    }

    private async Task<IActionResult> ToActionResultAsync(
        bool isSuccess,
        Enum? error,
        string message,
        VeterinarianClient? relationship,
        CancellationToken cancellationToken)
    {
        if (isSuccess && relationship is not null)
        {
            var veterinarian = await userQueryService.Handle(new GetUserByIdQuery(relationship.VeterinarianId), cancellationToken);
            return Ok(ToResource(relationship, veterinarian?.FullName));
        }

        if (error is ClientsError.VeterinarianClientNotFound) return NotFound(new { message });
        return Conflict(new { message });
    }

    /// <summary>
    ///     The rancher in the route must be the authenticated rancher.
    /// </summary>
    private bool IsCallerRancher(int rancherId, out IActionResult? denied)
    {
        var caller = HttpContext.GetCaller();
        if (caller is null)
        {
            denied = Unauthorized();
            return false;
        }

        if (!caller.IsRancher || caller.Id != rancherId)
        {
            denied = StatusCode(StatusCodes.Status403Forbidden);
            return false;
        }

        denied = null;
        return true;
    }

    private static VeterinarianRequestResource ToResource(VeterinarianClient relationship, string? veterinarianName)
    {
        return new VeterinarianRequestResource(
            relationship.Id,
            relationship.VeterinarianId,
            relationship.RancherId,
            veterinarianName ?? "Unknown veterinarian",
            relationship.Status,
            relationship.RequestedAt,
            relationship.AcceptedAt);
    }
}
