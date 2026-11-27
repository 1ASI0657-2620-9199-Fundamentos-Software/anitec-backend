using System.Net.Http.Headers;
using System.Net.Http.Json;
using Anitec.Platform.Notifications.Application.Internal.OutboundServices;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Notifications.Infrastructure.Email;

/// <summary>Settings of Resend. The key comes only from <c>Resend__ApiKey</c>: it is never in a file of the repository.</summary>
public class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>Empty turns the e-mail off: the alerts are still created and listed.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///     The sender. With the test sender of Resend, <c>onboarding@resend.dev</c>, only the address of the Resend
    ///     account itself receives the messages; sending to other addresses needs a verified domain.
    /// </summary>
    public string From { get; set; } = "AniTec <onboarding@resend.dev>";

    public string BaseUrl { get; set; } = "https://api.resend.com";

    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
///     Sends the e-mail through the REST API of Resend with a plain <see cref="HttpClient" />, so the project does not
///     carry another SDK. It never throws for a provider failure and never writes the key, the address or the body of
///     the message in its answer.
/// </summary>
public class ResendEmailNotificationService(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailNotificationService> logger) : IEmailNotificationService
{
    public async Task<EmailSendResult> SendAsync(AlertEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogDebug("Resend is not configured (Resend:ApiKey is empty): the e-mail is not sent");
            return EmailSendResult.Disabled;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/emails")
        {
            Content = JsonContent.Create(new
            {
                from = settings.From,
                to = new[] { email.To },
                subject = email.Subject,
                text = email.TextBody
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)));

            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode) return EmailSendResult.Sent;

            // Only the status code goes to the result: the body of an error can repeat the address of the person.
            return EmailSendResult.Failed($"Resend answered {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return EmailSendResult.Failed($"Resend did not answer in {settings.TimeoutSeconds} s");
        }
        catch (HttpRequestException exception)
        {
            return EmailSendResult.Failed($"Resend could not be reached ({exception.GetType().Name})");
        }
    }
}
