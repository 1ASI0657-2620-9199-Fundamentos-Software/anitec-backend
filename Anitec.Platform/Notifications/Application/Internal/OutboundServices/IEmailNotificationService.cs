namespace Anitec.Platform.Notifications.Application.Internal.OutboundServices;

/// <summary>An e-mail about an alert, in plain text.</summary>
public record AlertEmail(string To, string Subject, string TextBody);

public enum EmailSendStatus
{
    /// <summary>The provider accepted the message.</summary>
    Sent,

    /// <summary>The provider is not configured (no key): nothing was sent and nothing was tried.</summary>
    Disabled,

    /// <summary>The provider refused the message or could not be reached. It can be tried again.</summary>
    Failed
}

/// <param name="Status">What happened.</param>
/// <param name="Reason">A short reason for a failure. It never contains the key or the body of the message.</param>
public record EmailSendResult(EmailSendStatus Status, string? Reason = null)
{
    public static readonly EmailSendResult Sent = new(EmailSendStatus.Sent);
    public static readonly EmailSendResult Disabled = new(EmailSendStatus.Disabled, "No e-mail provider is configured.");

    public static EmailSendResult Failed(string reason)
    {
        return new EmailSendResult(EmailSendStatus.Failed, reason);
    }
}

/// <summary>
///     Outbound port to the e-mail provider (Resend). The application depends on this interface, as Iam does on
///     <c>ITokenService</c>, not on the provider. It never throws for a provider failure: it answers with a result.
/// </summary>
public interface IEmailNotificationService
{
    Task<EmailSendResult> SendAsync(AlertEmail email, CancellationToken cancellationToken);
}

/// <summary>Finds where to send the e-mail of a rancher.</summary>
public interface IOwnerEmailLookup
{
    /// <returns>The e-mail address of the user, or null when the user has none.</returns>
    Task<string?> FindEmailAsync(int userId, CancellationToken cancellationToken);
}
