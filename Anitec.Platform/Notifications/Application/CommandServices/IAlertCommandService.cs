using Anitec.Platform.Notifications.Domain.Model.Commands;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Shared.Application.Model;

namespace Anitec.Platform.Notifications.Application.CommandServices;

public interface IAlertCommandService
{
    /// <summary>
    ///     Creates an alert unless there is already one for the same source and due date; in that case it fails with
    ///     <c>AlertAlreadyExists</c>, which is not an error for the scanner.
    /// </summary>
    Task<Result<Alert>> Handle(CreateAlertCommand command, CancellationToken cancellationToken);

    Task<Result<Alert>> Handle(ConfirmAlertCommand command, CancellationToken cancellationToken);
    Task<Result<Alert>> Handle(SnoozeAlertCommand command, CancellationToken cancellationToken);
    Task<Result<Alert>> Handle(CloseAlertCommand command, CancellationToken cancellationToken);

    /// <returns>The alerts that came back to pending.</returns>
    Task<IReadOnlyList<Alert>> Handle(ReactivateSnoozedAlertsCommand command, CancellationToken cancellationToken);
}
