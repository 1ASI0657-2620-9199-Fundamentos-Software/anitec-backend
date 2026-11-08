using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Domain.Model.Commands;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Shared.Application.Model;

namespace Anitec.Subscriptions.Application.CommandServices;

/// <summary>The subscription and the payment that a paid checkout produced.</summary>
/// <param name="AlreadyProcessed">True when the session had been stored before (a repeated webhook or confirm).</param>
public record CheckoutOutcome(Subscription Subscription, Payment Payment, bool AlreadyProcessed);

public interface ICheckoutCommandService
{
    /// <summary>Opens a checkout for a plan. The amount is taken from the plan, never from the caller.</summary>
    Task<Result<CheckoutSession>> Handle(StartCheckoutCommand command, CancellationToken cancellationToken);

    /// <summary>
    ///     Fallback for the success page: reads the session at the provider and records it when the webhook has not
    ///     arrived yet. Only the user that opened the session can confirm it.
    /// </summary>
    Task<Result<CheckoutOutcome>> Handle(ConfirmCheckoutCommand command, CancellationToken cancellationToken);

    /// <summary>Records a paid checkout (called by the webhook and by confirm). Idempotent by session id.</summary>
    Task<Result<CheckoutOutcome>> Handle(CompleteCheckoutCommand command, CancellationToken cancellationToken);
}
