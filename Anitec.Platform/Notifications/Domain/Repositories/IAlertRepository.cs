using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Notifications.Domain.Repositories;

public interface IAlertRepository : IBaseRepository<Alert>
{
    /// <summary>Whether there is already an alert for this source and this due date, whatever its status.</summary>
    Task<bool> ExistsForSourceAsync(string sourceType, int sourceId, DateOnly sourceDueDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Alert>> ListByOwnerAsync(int ownerId, string? status, int limit,
        CancellationToken cancellationToken);

    /// <summary>Snoozed alerts whose new date is today or earlier.</summary>
    Task<IReadOnlyList<Alert>> ListSnoozedDueAsync(DateOnly today, CancellationToken cancellationToken);
}
