namespace Anitec.Platform.Notifications.Application;

/// <summary>Settings of the alerts. They come from <c>Notifications__*</c> variables.</summary>
public class NotificationsOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Turns the scanner on or off. Alerts that already exist keep working when it is off.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the scanner looks for due dates. 15 minutes in the design of iteration 5.</summary>
    public int ScanIntervalMinutes { get; set; } = 15;

    /// <summary>Wait before the first scan, so the migrations of the database finish first.</summary>
    public int StartDelaySeconds { get; set; } = 20;

    /// <summary>
    ///     Offset from UTC that decides what "today" is. Due dates are dates without a time, and a rancher in Peru
    ///     (UTC-5) should not get the alert of tomorrow at 7 p.m. of today.
    /// </summary>
    public int UtcOffsetHours { get; set; } = -5;

    /// <summary>What day it is for the ranchers.</summary>
    public DateOnly Today(TimeProvider clock)
    {
        return DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(UtcOffsetHours));
    }
}
