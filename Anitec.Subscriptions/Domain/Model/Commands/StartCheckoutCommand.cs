namespace Anitec.Subscriptions.Domain.Model.Commands;

public record StartCheckoutCommand(int UserId, int PlanId);
