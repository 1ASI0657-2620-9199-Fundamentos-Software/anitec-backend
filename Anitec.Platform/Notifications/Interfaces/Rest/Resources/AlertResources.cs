namespace Anitec.Platform.Notifications.Interfaces.Rest.Resources;

public record AlertResource(
    int Id,
    string SourceType,
    int SourceId,
    string Title,
    int? AnimalId,
    DateOnly DueDate,
    string Status,
    bool IsOverdue,
    string? ResolutionReason,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    bool EmailSent);

/// <param name="Reason">Why the alert is postponed. Required.</param>
/// <param name="Days">From 1 to 30. Three when it is not sent.</param>
public record SnoozeAlertResource(string? Reason, int? Days);

public record CloseAlertResource(string? Reason);
