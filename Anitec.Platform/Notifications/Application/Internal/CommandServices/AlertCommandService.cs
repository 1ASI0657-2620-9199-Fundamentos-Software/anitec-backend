using Anitec.Platform.Notifications.Application.CommandServices;
using Anitec.Platform.Notifications.Domain.Model;
using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Repositories;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Notifications.Application.Internal.CommandServices;

public class AlertCommandService(
    IAlertRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<NotificationsOptions> options,
    ILogger<AlertCommandService> logger) : IAlertCommandService
{
    public const int MaxSnoozeDays = 30;

    public async Task<Result<Alert>> Handle(CreateAlertCommand command, CancellationToken cancellationToken)
    {
        if (await repository.ExistsForSourceAsync(command.SourceType, command.SourceId, command.DueDate, cancellationToken))
            return Result<Alert>.Failure(NotificationsError.AlertAlreadyExists, "The alert already exists.");

        var alert = new Alert(command, clock.GetUtcNow().UtcDateTime);
        await repository.AddAsync(alert, cancellationToken);
        try
        {
            await unitOfWork.CompleteAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // The unique index on (source, source due date) is what stops two replicas from storing the same alert
            // when they scan at the same moment. The one that lost forgets its copy; the other one stored it.
            repository.Remove(alert);
            if (await repository.ExistsForSourceAsync(command.SourceType, command.SourceId, command.DueDate,
                    cancellationToken))
                return Result<Alert>.Failure(NotificationsError.AlertAlreadyExists, "The alert already exists.");

            logger.LogError(exception, "The alert of {SourceType} {SourceId} could not be stored", command.SourceType,
                command.SourceId);
            throw;
        }

        return Result<Alert>.Success(alert);
    }

    public async Task<Result<Alert>> Handle(ConfirmAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = await FindOwnAsync(command.AlertId, command.Caller, cancellationToken);
        if (alert is null) return NotFound();
        if (!alert.CanConfirm())
            return Result<Alert>.Failure(NotificationsError.InvalidTransition,
                $"An alert that is {alert.Status} cannot be confirmed.");

        alert.Confirm(command.Caller.Id, clock.GetUtcNow().UtcDateTime);
        repository.Update(alert);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Alert>.Success(alert);
    }

    public async Task<Result<Alert>> Handle(SnoozeAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = await FindOwnAsync(command.AlertId, command.Caller, cancellationToken);
        if (alert is null) return NotFound();
        if (string.IsNullOrWhiteSpace(command.Reason))
            return Result<Alert>.Failure(NotificationsError.ReasonRequired, "A snoozed alert needs a reason.");
        if (command.Days is < 1 or > MaxSnoozeDays)
            return Result<Alert>.Failure(NotificationsError.InvalidDays,
                $"The alert can be snoozed from 1 to {MaxSnoozeDays} days.");
        if (!alert.CanSnooze())
            return Result<Alert>.Failure(NotificationsError.InvalidTransition,
                $"An alert that is {alert.Status} cannot be snoozed.");

        var today = options.Value.Today(clock);
        alert.Snooze(command.Caller.Id, command.Reason.Trim(), today.AddDays(command.Days),
            clock.GetUtcNow().UtcDateTime);
        repository.Update(alert);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Alert>.Success(alert);
    }

    public async Task<Result<Alert>> Handle(CloseAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = await FindOwnAsync(command.AlertId, command.Caller, cancellationToken);
        if (alert is null) return NotFound();
        if (!alert.CanClose())
            return Result<Alert>.Failure(NotificationsError.InvalidTransition, "The alert is already closed.");

        alert.Close(command.Caller.Id, command.Reason?.Trim(), clock.GetUtcNow().UtcDateTime);
        repository.Update(alert);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<Alert>.Success(alert);
    }

    public async Task<IReadOnlyList<Alert>> Handle(ReactivateSnoozedAlertsCommand command,
        CancellationToken cancellationToken)
    {
        var due = await repository.ListSnoozedDueAsync(command.Today, cancellationToken);
        if (due.Count == 0) return due;

        foreach (var alert in due)
        {
            alert.Reactivate();
            repository.Update(alert);
        }

        await unitOfWork.CompleteAsync(cancellationToken);
        return due;
    }

    /// <summary>
    ///     Another rancher's alert looks exactly like one that does not exist (QAS-08): both are "not found".
    /// </summary>
    private async Task<Alert?> FindOwnAsync(int id, CallerContext caller, CancellationToken cancellationToken)
    {
        if (!caller.IsRancher) return null;

        var alert = await repository.FindByIdAsync(id, cancellationToken);
        return alert is not null && alert.OwnerId == caller.Id ? alert : null;
    }

    private static Result<Alert> NotFound()
    {
        return Result<Alert>.Failure(NotificationsError.AlertNotFound, "Alert not found.");
    }
}
