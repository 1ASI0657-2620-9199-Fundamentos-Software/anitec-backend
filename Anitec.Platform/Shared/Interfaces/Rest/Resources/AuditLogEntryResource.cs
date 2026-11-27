namespace Anitec.Platform.Shared.Interfaces.Rest.Resources;

public record AuditLogEntryResource(
    long Id,
    string? CorrelationId,
    int ActorUserId,
    string? ActorName,
    string ActorRole,
    string Action,
    string EntityType,
    int? EntityId,
    DateTime OccurredAt,
    string Result,
    string? Details);
