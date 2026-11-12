using System.Net.Mime;
using System.Text;
using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Subscriptions.Interfaces.Rest;

/// <summary>
///     Receives the events of Stripe. It has no [Authorize]: Stripe does not carry a user token. The request is
///     trusted only after the signature of the <c>Stripe-Signature</c> header is verified against the raw body.
/// </summary>
[ApiController]
[PaymentGatewayExceptionFilter]
[Route("api/v1/subscriptions/stripe/webhook")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Stripe webhook")]
public class StripeWebhookController(
    IPaymentGateway paymentGateway,
    ICheckoutCommandService checkoutService,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    private static readonly string[] PaidEvents = ["checkout.session.completed", "checkout.session.async_payment_succeeded"];

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // The signature covers the exact bytes Stripe sent, so the body is read as text and not as a model.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        PaymentWebhookEvent webhookEvent;
        try
        {
            webhookEvent = paymentGateway.ParseWebhook(payload, Request.Headers["Stripe-Signature"].FirstOrDefault());
        }
        catch (InvalidWebhookSignatureException exception)
        {
            // Only the reason is logged: never the secret, the header or the body.
            logger.LogWarning("Stripe webhook rejected: {Reason}", exception.InnerException?.Message ?? exception.Message);
            return BadRequest(new { message = "Invalid webhook signature." });
        }

        if (!PaidEvents.Contains(webhookEvent.Type) || webhookEvent.Session is not { IsPaid: true } session)
            return Ok(new { received = true, handled = false });

        var result = await checkoutService.Handle(
            new CompleteCheckoutCommand(session.Id, session.UserId, session.PlanId, session.CustomerId,
                session.SubscriptionId),
            cancellationToken);

        if (result.IsFailure)
        {
            // Answering 200 stops Stripe from retrying an event that can never be processed. A failure of the
            // database throws instead, answers 500, and Stripe retries it later.
            logger.LogWarning("Stripe event {EventId} was not applied: {Reason}", webhookEvent.Id, result.Message);
            return Ok(new { received = true, handled = false });
        }

        return Ok(new { received = true, handled = true, duplicate = result.Value!.AlreadyProcessed });
    }
}
