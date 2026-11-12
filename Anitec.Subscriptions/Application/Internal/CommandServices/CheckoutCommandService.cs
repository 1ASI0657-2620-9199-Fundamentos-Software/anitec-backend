using Anitec.Subscriptions.Application.CommandServices;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Domain.Model;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Anitec.Subscriptions.Shared.Application.Model;
using Anitec.Subscriptions.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Subscriptions.Application.Internal.CommandServices;

public class CheckoutCommandService(
    ISubscriptionPlanRepository planRepository,
    ISubscriptionRepository subscriptionRepository,
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPaymentGateway paymentGateway,
    TimeProvider timeProvider,
    ILogger<CheckoutCommandService> logger) : ICheckoutCommandService
{
    private const string Currency = "PEN";
    private const string Provider = "StripeTest";

    public async Task<Result<CheckoutSession>> Handle(StartCheckoutCommand command, CancellationToken cancellationToken)
    {
        var plan = await planRepository.FindByIdAsync(command.PlanId, cancellationToken);
        if (plan is null || !plan.IsActive)
            return Result<CheckoutSession>.Failure(SubscriptionsError.SubscriptionPlanNotFound,
                "Subscription plan not found.");

        var session = await paymentGateway.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest(command.UserId, plan.Id, plan.Name, plan.MaxAnimals, plan.Price, Currency),
            cancellationToken);

        return Result<CheckoutSession>.Success(session);
    }

    public async Task<Result<CheckoutOutcome>> Handle(ConfirmCheckoutCommand command, CancellationToken cancellationToken)
    {
        var stored = await paymentRepository.FindByProviderPaymentIdAsync(command.SessionId, cancellationToken);
        if (stored is not null)
        {
            // A session paid by another user looks like a session that does not exist (QAS-08).
            if (stored.UserId != command.UserId) return CheckoutNotFound();
            return await OutcomeOfStoredPayment(stored, cancellationToken);
        }

        var details = await paymentGateway.GetCheckoutSessionAsync(command.SessionId, cancellationToken);
        if (details is null || details.UserId != command.UserId) return CheckoutNotFound();

        if (!details.IsPaid)
            return Result<CheckoutOutcome>.Failure(SubscriptionsError.CheckoutNotPaid,
                "The checkout session has not been paid.");

        return await Handle(
            new CompleteCheckoutCommand(details.Id, details.UserId, details.PlanId, details.CustomerId,
                details.SubscriptionId),
            cancellationToken);
    }

    public async Task<Result<CheckoutOutcome>> Handle(CompleteCheckoutCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId || command.PlanId is not { } planId)
            return Result<CheckoutOutcome>.Failure(SubscriptionsError.CheckoutMetadataIncomplete,
                "The checkout session was not opened by this service.");

        var plan = await planRepository.FindByIdAsync(planId, cancellationToken);
        if (plan is null)
            return Result<CheckoutOutcome>.Failure(SubscriptionsError.SubscriptionPlanNotFound,
                "Subscription plan not found.");

        var stored = await paymentRepository.FindByProviderPaymentIdAsync(command.SessionId, cancellationToken);
        if (stored is not null) return await OutcomeOfStoredPayment(stored, cancellationToken);

        try
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var today = DateOnly.FromDateTime(now);
            var providerSubscriptionId = string.IsNullOrWhiteSpace(command.SubscriptionId)
                ? command.SessionId
                : command.SubscriptionId;

            // A previous attempt may have stored the subscription and failed before the payment: reuse it.
            var subscription = await subscriptionRepository.FindByStripeSubscriptionIdAsync(providerSubscriptionId,
                cancellationToken);
            if (subscription is null)
            {
                var current = await subscriptionRepository.FindByUserIdAsync(userId, cancellationToken);
                foreach (var previous in current.Where(item =>
                             item.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)))
                {
                    previous.Status = "Inactive";
                    previous.EndsAt = today;
                    subscriptionRepository.Update(previous);
                }

                subscription = new Subscription(new CreateSubscriptionCommand(
                    userId, plan.Id, command.CustomerId ?? string.Empty, providerSubscriptionId, "Active", today, null));
                await subscriptionRepository.AddAsync(subscription, cancellationToken);
                await unitOfWork.CompleteAsync(cancellationToken);
            }

            var payment = new Payment(new CreatePaymentCommand(
                userId, subscription.Id, plan.Price, Currency, Provider, command.SessionId, "Paid", now));
            await paymentRepository.AddAsync(payment, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);

            return Result<CheckoutOutcome>.Success(new CheckoutOutcome(subscription, payment, false));
        }
        catch (DbUpdateException exception)
        {
            // The unique indexes on the session and on the provider subscription stop a concurrent webhook and
            // confirm from storing the same checkout twice. The one that lost reads what the other one stored.
            var winner = await paymentRepository.FindByProviderPaymentIdAsync(command.SessionId, cancellationToken);
            if (winner is not null) return await OutcomeOfStoredPayment(winner, cancellationToken);

            logger.LogError(exception, "Checkout {SessionId} could not be stored", command.SessionId);
            throw;
        }
    }

    private async Task<Result<CheckoutOutcome>> OutcomeOfStoredPayment(Payment payment, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.FindByIdAsync(payment.SubscriptionId, cancellationToken);
        return subscription is null
            ? Result<CheckoutOutcome>.Failure(SubscriptionsError.SubscriptionNotFound, "Subscription not found.")
            : Result<CheckoutOutcome>.Success(new CheckoutOutcome(subscription, payment, true));
    }

    private static Result<CheckoutOutcome> CheckoutNotFound()
    {
        return Result<CheckoutOutcome>.Failure(SubscriptionsError.CheckoutNotFound, "Checkout session not found.");
    }
}
