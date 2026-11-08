using System.Net.Mime;
using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Application.QueryServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Queries;
using Anitec.Subscriptions.Interfaces.Rest.Resources;
using Anitec.Subscriptions.Interfaces.Rest.Transform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Stripe;
using Stripe.Checkout;

namespace Anitec.Subscriptions.Interfaces.Rest;

[Authorize(Roles = "Rancher,Veterinarian")]
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
    IConfiguration configuration) : ControllerBase
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

    [HttpPost("stripe-checkout")]
    public async Task<IActionResult> CreateStripeCheckout(
        StripeCheckoutResource resource,
        CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var plan = await planQueryService.Handle(new GetSubscriptionPlanByIdQuery(resource.PlanId), cancellationToken);
        if (plan is null || !plan.IsActive) return NotFound(new { message = "Subscription plan not found." });

        var secretKey = configuration["StripeSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Contains("change_me", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Stripe test secret key is not configured." });

        var successUrl = configuration["StripeSettings:SuccessUrl"];
        var cancelUrl = configuration["StripeSettings:CancelUrl"];
        if (string.IsNullOrWhiteSpace(successUrl) || string.IsNullOrWhiteSpace(cancelUrl))
            return BadRequest(new { message = "Stripe success and cancel URLs are not configured." });

        StripeConfiguration.ApiKey = secretKey;

        var unitAmount = (long)Math.Round(plan.Price * 100, MidpointRounding.AwayFromZero);
        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = caller.Id.ToString(),
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = caller.Id.ToString(),
                ["planId"] = plan.Id.ToString()
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "pen",
                        UnitAmount = unitAmount,
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = "month"
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = plan.Name,
                            Description = $"AniTec subscription plan for up to {plan.MaxAnimals} animals"
                        }
                    }
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return Ok(new StripeCheckoutSessionResource(session.Id, session.Url));
    }

    [HttpGet("stripe-checkout/{sessionId}/confirm")]
    public async Task<IActionResult> ConfirmStripeCheckout(string sessionId, CancellationToken cancellationToken)
    {
        var caller = User.GetCaller();
        if (caller is null) return Unauthorized();

        var existingPayment = await paymentQueryService.Handle(sessionId, cancellationToken);
        if (existingPayment is not null)
        {
            // A session paid by another user answers 404 (QAS-08).
            if (existingPayment.UserId != caller.Id) return NotFound();

            var existingSubscription = await queryService.Handle(
                new GetSubscriptionByIdQuery(existingPayment.SubscriptionId),
                cancellationToken);

            if (existingSubscription is null) return NotFound();

            return Ok(new StripeCheckoutResultResource(
                SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(existingSubscription),
                PaymentResourceFromEntityAssembler.ToResourceFromEntity(existingPayment)));
        }

        var secretKey = configuration["StripeSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Contains("change_me", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Stripe test secret key is not configured." });

        StripeConfiguration.ApiKey = secretKey;
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, cancellationToken: cancellationToken);

        if (!session.Status.Equals("complete", StringComparison.OrdinalIgnoreCase) &&
            !session.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Stripe checkout session is not completed." });

        if (!session.Metadata.TryGetValue("userId", out var userIdValue) ||
            !int.TryParse(userIdValue, out var stripeUserId) ||
            stripeUserId != caller.Id)
            return NotFound();

        if (!session.Metadata.TryGetValue("planId", out var planIdValue) ||
            !int.TryParse(planIdValue, out var planId))
            return BadRequest(new { message = "Stripe checkout metadata is incomplete." });

        var plan = await planQueryService.Handle(new GetSubscriptionPlanByIdQuery(planId), cancellationToken);
        if (plan is null) return NotFound(new { message = "Subscription plan not found." });

        var subscriptions = await queryService.Handle(new GetSubscriptionsByUserQuery(caller.Id), cancellationToken);
        var activeSubscriptions = subscriptions
            .Where(item => item.Status.Equals("Active", StringComparison.OrdinalIgnoreCase));

        foreach (var subscription in activeSubscriptions)
        {
            var updateCommand = new UpdateSubscriptionCommand(
                subscription.Id,
                subscription.UserId,
                subscription.PlanId,
                subscription.StripeCustomerId,
                subscription.StripeSubscriptionId,
                "Inactive",
                subscription.StartedAt,
                DateOnly.FromDateTime(DateTime.UtcNow));

            await commandService.Handle(updateCommand, cancellationToken);
        }

        var stripeCustomerId = session.CustomerId ?? string.Empty;
        var stripeSubscriptionId = session.SubscriptionId ?? session.Id;
        var subscriptionCommand = new CreateSubscriptionCommand(
            caller.Id,
            plan.Id,
            stripeCustomerId,
            stripeSubscriptionId,
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
            "StripeTest",
            session.Id,
            "Paid",
            DateTime.UtcNow);

        var paymentResult = await paymentCommandService.Handle(paymentCommand, cancellationToken);
        if (paymentResult.IsFailure) return BadRequest(new { message = paymentResult.Message });

        return Ok(new StripeCheckoutResultResource(
            SubscriptionResourceFromEntityAssembler.ToResourceFromEntity(subscriptionResult.Value),
            PaymentResourceFromEntityAssembler.ToResourceFromEntity(paymentResult.Value!)));
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

