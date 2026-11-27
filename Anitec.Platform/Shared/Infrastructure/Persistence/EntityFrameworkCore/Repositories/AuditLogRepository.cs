using Anitec.Platform.Shared.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await context.Set<AuditLogEntry>().AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListByOwnerAsync(
        int ownerUserId,
        string? action,
        string? entityType,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The owner filter is the first condition and is not optional: it is what keeps the rows of one rancher
        // away from another one.
        var query = context.Set<AuditLogEntry>().AsNoTracking().Where(entry => entry.OwnerUserId == ownerUserId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(entry => entry.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(entry => entry.EntityType == entityType);
        if (from is not null) query = query.Where(entry => entry.OccurredAt >= from);
        if (to is not null) query = query.Where(entry => entry.OccurredAt <= to);

        return await query.OrderByDescending(entry => entry.OccurredAt).ThenByDescending(entry => entry.Id)
            .Take(limit).ToListAsync(cancellationToken);
    }
}
