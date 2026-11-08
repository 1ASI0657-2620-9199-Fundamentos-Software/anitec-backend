namespace Anitec.Subscriptions.Domain.Model.Commands;

public record ConfirmCheckoutCommand(int UserId, string SessionId);
