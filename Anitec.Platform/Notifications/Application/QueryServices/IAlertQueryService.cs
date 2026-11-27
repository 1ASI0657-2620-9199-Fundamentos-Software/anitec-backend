using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Model.Queries;

namespace Anitec.Platform.Notifications.Application.QueryServices;

public interface IAlertQueryService
{
    Task<IReadOnlyList<Alert>> Handle(GetAlertsForCallerQuery query, CancellationToken cancellationToken);
    Task<Alert?> Handle(GetAlertByIdForCallerQuery query, CancellationToken cancellationToken);
}
