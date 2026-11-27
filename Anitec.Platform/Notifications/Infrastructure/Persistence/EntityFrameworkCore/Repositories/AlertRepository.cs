using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Platform.Notifications.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class AlertRepository(AppDbContext context) : BaseRepository<Alert>(context), IAlertRepository
{
    public async Task<bool> ExistsForSourceAsync(string sourceType, int sourceId, DateOnly sourceDueDate,
        CancellationToken cancellationToken)
    {
        return await Context.Set<Alert>().AnyAsync(
            alert => alert.SourceType == sourceType && alert.SourceId == sourceId &&
                     alert.SourceDueDate == sourceDueDate, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> ListByOwnerAsync(int ownerId, string? status, int limit,
        CancellationToken cancellationToken)
    {
        var query = Context.Set<Alert>().Where(alert => alert.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(alert => alert.Status == status);

        return await query.OrderBy(alert => alert.DueDate).ThenBy(alert => alert.Id).Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> ListSnoozedDueAsync(DateOnly today, CancellationToken cancellationToken)
    {
        return await Context.Set<Alert>()
            .Where(alert => alert.Status == AlertStatus.Snoozed && alert.DueDate <= today)
            .ToListAsync(cancellationToken);
    }
}
