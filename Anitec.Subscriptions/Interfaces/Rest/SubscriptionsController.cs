using System.Net.Mime;
using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Application.QueryServices;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Queries;
using Anitec.Subscriptions.Interfaces.Rest.Resources;
using Anitec.Subscriptions.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Anitec.Subscriptions.Interfaces.Rest;

[Authorize(Roles = "Rancher,Veterinarian")]
[PaymentGatewayExceptionFilter]
[ApiController]
[Route("api/v1/subscriptions")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Subscriptions endpoints")]
public class SubscriptionsController(
    ISubscriptionCommandService commandService,
    ISubscriptionQueryService queryService,
    ISubscriptionPlanQueryService planQueryService,
    IPaymentCommandService paymentCommandService,
    IPaymentQueryService paymentQueryService,
    ICheckoutCommandService checkoutService) : ControllerBase
{
    // A user only sees its own subscriptions. The identity comes from the token, never from the request.
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetSubscriptionsByUserQuery(caller.Id), cancellationToken);
        return Ok(result.Select(SubscriptionResourceFromEntityAssembler.ToResourceFromEntity));
    }

    // A subscription of another user answers 404, as if it did not exist (QAS-08).
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await queryService.Handle(new GetSubscriptionByIdQuery(id), cancellationToken);
        if (result is null || result.UserId != caller.Id) return NotFound();
        return Ok(SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(result));
    }

    [DevelopmentOnly]
    [HttpPost]
    public async Task<IActionResult> Create(CreateSubscriptionResource resource, CancellationToken cancellationToken)
    {
        var command = CreateSubscriptionCommandFromResourceAssembler.ToCommandFromResource(resource);
        var result = await commandService.Handle(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id },
            SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(result.Value));
    }

    [HttpGet("users/{userId:int}/active")]
    public async Task<IActionResult> GetActiveByUser(int userId, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();
        if (userId != caller.Id) return NotFound();

        var subscription = await queryService.Handle(new GetActiveSubscriptionByUserQuery(caller.Id), cancellationToken);
        if (subscription is null) return NotFound();

        return Ok(SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(subscription));
    }

    [HttpGet("users/{userId:int}/payments")]
    public async Task<IActionResult> GetPaymentsByUser(int userId, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();
        if (userId != caller.Id) return NotFound();

        var payments = await paymentQueryService.Handle(caller.Id, cancellationToken);
        return Ok(payments.Select(PaymentResourceFromEntityAssembler.ToResourceFromEntity));
    }

    // Demo data for Development only. The user comes from the token and the amount from the plan:
    // nothing about who pays or how much is taken from the body.
    [DevelopmentOnly]
    [HttpPost("mock-checkout")]
    public async Task<IActionResult> MockCheckout(MockCheckoutResource resource, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var plan = await planQueryService.Handle(new GetSubscriptionPlanByIdQuery(resource.PlanId), cancellationToken);
        if (plan is null || !plan.IsActive) return NotFound(new { message = "Subscription plan not found." });

        var subscriptionCommand = new CreateSubscriptionCommand(
            caller.Id,
            plan.Id,
            $"mock_customer_{caller.Id}",
            $"mock_subscription_{Guid.NewGuid():N}",
            "Active",
            DateOnly.FromDateTime(DateTime.UtcNow),
            null);

        var subscriptionResult = await commandService.Handle(subscriptionCommand, cancellationToken);
        if (subscriptionResult.IsFailure) return BadRequest(new { message = subscriptionResult.Message });

        var paymentCommand = new CreatePaymentCommand(
            caller.Id,
            subscriptionResult.Value!.Id,
            plan.Price,
            "PEN",
            "MockStripe",
            $"mock_payment_{Guid.NewGuid():N}",
            "Paid",
            DateTime.UtcNow);

        var paymentResult = await paymentCommandService.Handle(paymentCommand, cancellationToken);
        if (paymentResult.IsFailure) return BadRequest(new { message = paymentResult.Message });

        return Ok(new MockCheckoutResultResource(
            SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(subscriptionResult.Value),
            PaymentResourceFromEntityAssembler.ToResourceFromEntity(paymentResult.Value!)));
    }

    // The amount comes from the plan and the buyer from the token: the body only names the plan.
    [HttpPost("stripe-checkout")]
    public async Task<IActionResult> CreateStripeCheckout(
        StripeCheckoutResource resource,
        CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await checkoutService.Handle(new StartCheckoutCommand(caller.Id, resource.PlanId),
            cancellationToken);
        if (result.IsFailure) return NotFound(new { message = result.Message });

        return Ok(new StripeCheckoutSessionResource(result.Value!.Id, result.Value.Url));
    }

    // Fallback of the success page. The webhook is the main way a payment is recorded; this endpoint stores the
    // session when the webhook has not arrived yet and returns what is already stored otherwise.
    [HttpGet("stripe-checkout/{sessionId}/confirm")]
    public async Task<IActionResult> ConfirmStripeCheckout(string sessionId, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var result = await checkoutService.Handle(new ConfirmCheckoutCommand(caller.Id, sessionId), cancellationToken);
        if (result.IsFailure)
            return result.Error switch
            {
                SubscriptionsError.CheckoutNotPaid or SubscriptionsError.CheckoutMetadataIncomplete =>
                    BadRequest(new { message = result.Message }),
                _ => NotFound(new { message = result.Message })
            };

        return Ok(new StripeCheckoutResultResource(
            SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(result.Value!.Subscription),
            PaymentResourceFromEntityAssembler.ToResourceFromEntity(result.Value.Payment)));
    }

    [DevelopmentOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateSubscriptionResource resource, CancellationToken cancellationToken)
    {
        var command = UpdateSubscriptionCommandFromResourceAssembler.ToCommandFromResource(id, resource);
        var result = await commandService.Handle(command, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(result.Value!));
    }

    [DevelopmentOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await commandService.Handle(new DeleteSubscriptionCommand(id), cancellationToken);
        if (result.IsFailure) return NotFound();
        return NoContent();
    }
}

