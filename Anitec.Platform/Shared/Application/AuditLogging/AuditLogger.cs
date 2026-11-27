using System.Text.Json;
using Anitec.Platform.Shared.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Shared.Application.AuditLogging;

public class AuditLogger(
    IAuditLogRepository repository,
    IUnitOfWork unitOfWork,
    ICorrelationIdAccessor correlation,
    TimeProvider timeProvider,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    public async Task RecordAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await repository.AddAsync(new AuditLogEntry
            {
                CorrelationId = correlation.CorrelationId,
                ActorUserId = record.Actor.Id,
                ActorRole = record.Actor.Role,
                OwnerUserId = record.OwnerUserId,
                Action = record.Action,
                EntityType = record.EntityType,
                EntityId = record.EntityId,
                OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
                Result = AuditResults.Success,
                DetailsJson = record.Details is null ? null : JsonSerializer.Serialize(record.Details)
            }, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The operation already happened: failing the request now would tell the user it did not. The failure
            // is written to the log, where it can be found by the correlation id, so it does not go unnoticed.
            logger.LogError(exception, "The audit entry {Action} on {EntityType} {EntityId} could not be stored",
                record.Action, record.EntityType, record.EntityId);
        }
    }
}
