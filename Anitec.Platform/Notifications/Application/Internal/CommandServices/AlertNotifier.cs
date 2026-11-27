using Anitec.Platform.Notifications.Application.Internal.OutboundServices;
using Anitec.Platform.Notifications.Domain.Model.Entities;
using Anitec.Platform.Notifications.Domain.Repositories;
using Anitec.Platform.Shared.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Notifications.Application.Internal.CommandServices;

/// <summary>What one pass of the notifier did.</summary>
public record AlertNotificationResult(int Sent, int Failed, int WithoutAddress, int Skipped);

public interface IAlertNotifier
{
    Task<AlertNotificationResult> NotifyPendingAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Sends the e-mail of the alerts that were not notified yet. The e-mail is an extra channel over the alert, never
///     the source of truth (4.3.5.4): an alert exists and can be listed whether or not its e-mail is sent. A failure
///     leaves the alert as it is; the next pass tries again, up to <see cref="MaxAttempts" /> times so that a
///     rejected address does not make the notifier insist forever.
/// </summary>
public class AlertNotifier(
    IAlertRepository repository,
    IUnitOfWork unitOfWork,
    IEmailNotificationService email,
    IOwnerEmailLookup emails,
    TimeProvider clock,
    IOptions<NotificationsOptions> options,
    ILogger<AlertNotifier> logger) : IAlertNotifier
{
    public const int MaxAttempts = 3;
    private const int BatchSize = 50;

    public async Task<AlertNotificationResult> NotifyPendingAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var failed = 0;
        var withoutAddress = 0;
        var skipped = 0;

        foreach (var alert in await repository.ListToNotifyAsync(MaxAttempts, BatchSize, cancellationToken))
        {
            var address = await emails.FindEmailAsync(alert.OwnerId, cancellationToken);
            if (string.IsNullOrWhiteSpace(address))
            {
                // Nobody to write to: trying again would not change that, so the attempts are used up.
                alert.NotificationAttempts = MaxAttempts;
                repository.Update(alert);
                await unitOfWork.CompleteAsync(cancellationToken);
                withoutAddress++;
                logger.LogWarning("Alert {AlertId} has no e-mail address to notify; the alert is still listed in the API",
                    alert.Id);
                continue;
            }

            var result = await email.SendAsync(Compose(alert, address, options.Value), cancellationToken);
            switch (result.Status)
            {
                case EmailSendStatus.Disabled:
                    // No provider: nothing was tried, so no attempt is spent. When a key is set, it will be sent.
                    skipped++;
                    break;
                case EmailSendStatus.Sent:
                    alert.MarkNotified(clock.GetUtcNow().UtcDateTime);
                    repository.Update(alert);
                    await unitOfWork.CompleteAsync(cancellationToken);
                    sent++;
                    break;
                default:
                    alert.MarkNotificationFailed();
                    repository.Update(alert);
                    await unitOfWork.CompleteAsync(cancellationToken);
                    failed++;
                    logger.LogWarning(
                        "The e-mail of alert {AlertId} was not sent (attempt {Attempt} of {Max}): {Reason}",
                        alert.Id, alert.NotificationAttempts, MaxAttempts, result.Reason);
                    break;
            }
        }

        return new AlertNotificationResult(sent, failed, withoutAddress, skipped);
    }

    private static AlertEmail Compose(Alert alert, string address, NotificationsOptions options)
    {
        var what = alert.SourceType == AlertSourceTypes.HealthEvent ? "Seguimiento sanitario" : "Actividad";
        var subject = $"AniTec: {what} pendiente - {alert.Title}";
        var body = $"""
                    Hola,

                    {what} "{alert.Title}" vence el {alert.DueDate:dd/MM/yyyy} y todavía nadie lo confirmó.

                    Ingresa a AniTec para confirmarlo, posponerlo con un motivo o cerrarlo.{(string.IsNullOrWhiteSpace(options.AppUrl) ? string.Empty : $"\n{options.AppUrl.TrimEnd('/')}/")}

                    AniTec
                    """;
        return new AlertEmail(address, subject, body);
    }
}
