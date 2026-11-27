using Anitec.Platform.Notifications.Domain.Model.Commands;

namespace Anitec.Platform.Notifications.Domain.Model.Entities;

public static class AlertStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Snoozed = "Snoozed";
    public const string Closed = "Closed";
}

public static class AlertSourceTypes
{
    public const string HealthEvent = "HealthEvent";
    public const string FarmActivity = "FarmActivity";
}

/// <summary>
///     An operational alert about a due date that nobody confirmed (US-070, QAS-03). It is derived from a health
///     event or a farm activity: it keeps a reference to the source and the little that it needs to manage itself,
///     never a copy of the clinical data.
/// </summary>
public class Alert
{
    public Alert()
    {
        SourceType = string.Empty;
        Title = string.Empty;
        Status = AlertStatus.Pending;
    }

    public Alert(CreateAlertCommand command, DateTime now)
    {
        SourceType = command.SourceType;
        SourceId = command.SourceId;
        SourceDueDate = command.DueDate;
        OwnerId = command.OwnerId;
        Title = command.Title;
        AnimalId = command.AnimalId;
        DueDate = command.DueDate;
        Status = AlertStatus.Pending;
        CreatedAt = now;
    }

    public int Id { get; set; }

    /// <summary>"HealthEvent" or "FarmActivity".</summary>
    public string SourceType { get; set; }

    public int SourceId { get; set; }

    /// <summary>
    ///     The due date of the source when the alert was created. It never changes, and together with the source it
    ///     is unique: the scanner cannot create the same alert twice, not even from two replicas at the same time.
    ///     If somebody moves the due date of the source, the new date gets its own alert.
    /// </summary>
    public DateOnly SourceDueDate { get; set; }

    /// <summary>The rancher that has to attend the alert.</summary>
    public int OwnerId { get; set; }

    public string Title { get; set; }

    public int? AnimalId { get; set; }

    /// <summary>When the alert is due. Snoozing moves it forward.</summary>
    public DateOnly DueDate { get; set; }

    public string Status { get; set; }

    /// <summary>Why the alert was snoozed or closed.</summary>
    public string? ResolutionReason { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public int? ResolvedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the e-mail about the alert was sent. Null while it was not (or could not be) sent.</summary>
    public DateTime? NotifiedAt { get; set; }

    public int NotificationAttempts { get; set; }

    /// <summary>A pending alert whose date has passed is overdue: it stays visible until somebody attends it.</summary>
    public bool IsOverdue(DateOnly today)
    {
        return Status == AlertStatus.Pending && DueDate < today;
    }

    public bool CanConfirm()
    {
        return Status is AlertStatus.Pending or AlertStatus.Snoozed;
    }

    public bool CanSnooze()
    {
        return Status == AlertStatus.Pending;
    }

    public bool CanClose()
    {
        return Status != AlertStatus.Closed;
    }

    public void Confirm(int actorId, DateTime now)
    {
        Status = AlertStatus.Confirmed;
        ResolvedByUserId = actorId;
        ResolvedAt = now;
    }

    public void Snooze(int actorId, string reason, DateOnly newDueDate, DateTime now)
    {
        Status = AlertStatus.Snoozed;
        DueDate = newDueDate;
        ResolutionReason = reason;
        ResolvedByUserId = actorId;
        ResolvedAt = now;
    }

    public void Close(int actorId, string? reason, DateTime now)
    {
        Status = AlertStatus.Closed;
        ResolutionReason = string.IsNullOrWhiteSpace(reason) ? ResolutionReason : reason;
        ResolvedByUserId = actorId;
        ResolvedAt = now;
    }

    /// <summary>A snoozed alert comes back when its new date arrives.</summary>
    public void Reactivate()
    {
        Status = AlertStatus.Pending;
        NotifiedAt = null;
        NotificationAttempts = 0;
    }

    public void MarkNotified(DateTime now)
    {
        NotifiedAt = now;
        NotificationAttempts++;
    }

    public void MarkNotificationFailed()
    {
        NotificationAttempts++;
    }
}
