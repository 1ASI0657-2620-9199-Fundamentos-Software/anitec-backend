using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Notifications.Domain.Model.Commands;

public record CreateAlertCommand(
    string SourceType,
    int SourceId,
    int OwnerId,
    string Title,
    int? AnimalId,
    DateOnly DueDate);

public record ConfirmAlertCommand(int AlertId, CallerContext Caller);

/// <param name="Reason">Why the alert is postponed. It is required: a justified postponement (US-070).</param>
/// <param name="Days">How many days from today. Between 1 and 30.</param>
public record SnoozeAlertCommand(int AlertId, CallerContext Caller, string Reason, int Days);

public record CloseAlertCommand(int AlertId, CallerContext Caller, string? Reason);

/// <summary>Brings back the snoozed alerts whose new date has arrived.</summary>
public record ReactivateSnoozedAlertsCommand(DateOnly Today);
