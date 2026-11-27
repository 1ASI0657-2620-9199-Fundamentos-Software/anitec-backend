using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Shared.Application.AuditLogging;

/// <summary>What happened, who did it and whose data it touched.</summary>
/// <param name="Actor">The authenticated user that performed the operation.</param>
/// <param name="OwnerUserId">The rancher that owns the data. A rancher can read only the rows with its own id.</param>
/// <param name="Action">One of <c>AuditActions</c>.</param>
/// <param name="EntityType">The kind of record, such as "Animal" or "HealthEvent".</param>
/// <param name="EntityId">The id of the record, when there is only one.</param>
/// <param name="Details">Optional small object that is stored as JSON. It must not contain secrets.</param>
public record AuditRecord(
    CallerContext Actor,
    int OwnerUserId,
    string Action,
    string EntityType,
    int? EntityId = null,
    object? Details = null);

public interface IAuditLogger
{
    /// <summary>
    ///     Stores one entry after the operation succeeded. A failure to store it is logged, with the correlation id,
    ///     and does not undo the operation that already happened.
    /// </summary>
    Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}

/// <summary>Gives the code of the application the correlation id of the request that is running, if there is one.</summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
