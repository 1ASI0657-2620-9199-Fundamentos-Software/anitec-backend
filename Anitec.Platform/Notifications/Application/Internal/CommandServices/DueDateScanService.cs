using Anitec.Platform.Activities.Application.QueryServices;
using Anitec.Platform.Activities.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Notifications.Application.CommandServices;
using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Sanitary.Application.QueryServices;
using Anitec.Platform.Sanitary.Domain.Model.Queries;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Notifications.Application.Internal.CommandServices;

/// <summary>What one pass of the scanner did.</summary>
/// <param name="Created">The alerts that did not exist and were created now.</param>
/// <param name="Reactivated">The snoozed alerts whose new date arrived.</param>
public record DueDateScanResult(IReadOnlyList<Alert> Created, IReadOnlyList<Alert> Reactivated);

public interface IDueDateScanService
{
    Task<DueDateScanResult> ScanAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Looks for due dates that arrived and creates the alert of each one (QAS-03, iteration 5). It reads health
///     events and farm activities through their query services, the same ones the API uses, so the rule of what is
///     "due" lives in one place. Running it twice, or from two replicas, does not duplicate alerts: the command
///     service refuses a source and due date that already has one.
/// </summary>
public class DueDateScanService(
    IHealthEventQueryService healthEvents,
    IFarmActivityQueryService farmActivities,
    IAnimalQueryService animals,
    IAlertCommandService alerts,
    TimeProvider clock,
    IOptions<NotificationsOptions> options) : IDueDateScanService
{
    /// <summary>The activity status that means that nothing is left to do.</summary>
    public const string CompletedActivityStatus = "Completado";

    public async Task<DueDateScanResult> ScanAsync(CancellationToken cancellationToken)
    {
        var today = options.Value.Today(clock);
        var created = new List<Alert>();

        // 1. Health events with a follow-up date that arrived. The owner is the owner of the animal.
        var ownerOfAnimal = new Dictionary<int, int?>();
        foreach (var healthEvent in await healthEvents.Handle(new GetAllHealthEventsQuery(), cancellationToken))
        {
            if (healthEvent.NextDueDate is not { } dueDate || dueDate > today) continue;

            if (!ownerOfAnimal.TryGetValue(healthEvent.AnimalId, out var ownerId))
            {
                ownerId = await animals.Handle(new GetAnimalOwnerIdQuery(healthEvent.AnimalId), cancellationToken);
                ownerOfAnimal[healthEvent.AnimalId] = ownerId;
            }

            if (ownerId is null) continue;

            var result = await alerts.Handle(new CreateAlertCommand(AlertSourceTypes.HealthEvent, healthEvent.Id,
                ownerId.Value, Shorten(healthEvent.Type), healthEvent.AnimalId, dueDate), cancellationToken);
            if (result.IsSuccess) created.Add(result.Value!);
        }

        // 2. Farm activities whose date arrived and are not completed.
        foreach (var activity in await farmActivities.Handle(new GetAllFarmActivitiesQuery(), cancellationToken))
        {
            if (activity.OwnerId is not { } ownerId || activity.Date > today) continue;
            if (activity.Status.Equals(CompletedActivityStatus, StringComparison.OrdinalIgnoreCase)) continue;

            var result = await alerts.Handle(new CreateAlertCommand(AlertSourceTypes.FarmActivity, activity.Id,
                ownerId, Shorten(activity.Title), null, activity.Date), cancellationToken);
            if (result.IsSuccess) created.Add(result.Value!);
        }

        // 3. Snoozed alerts come back when their new date arrives.
        var reactivated = await alerts.Handle(new ReactivateSnoozedAlertsCommand(today), cancellationToken);

        return new DueDateScanResult(created, reactivated);
    }

    private static string Shorten(string text)
    {
        var clean = (text ?? string.Empty).Trim();
        return clean.Length <= 120 ? clean : clean[..120];
    }
}
