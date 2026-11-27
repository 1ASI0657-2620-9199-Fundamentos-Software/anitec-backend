using Anitec.Platform.Shared.Domain.Model.Entities;

namespace Anitec.Platform.Shared.Domain.Repositories;

/// <summary>
///     Storage of the audit log. It does not extend <see cref="IBaseRepository{TEntity}" /> on purpose: there is no
///     way to update or remove an entry through it.
/// </summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogEntry>> ListByOwnerAsync(
        int ownerUserId,
        string? action,
        string? entityType,
        DateTime? from,
        DateTime? to,
        int limit,
        CancellationToken cancellationToken = default);
}
