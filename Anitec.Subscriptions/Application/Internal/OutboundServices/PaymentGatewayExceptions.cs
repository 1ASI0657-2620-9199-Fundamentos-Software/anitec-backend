namespace Anitec.Subscriptions.Application.Internal.OutboundServices;

/// <summary>The keys or URLs of the payment provider are not configured in this environment.</summary>
public class PaymentGatewayNotConfiguredException(string message) : Exception(message);

/// <summary>The payment provider rejected a request or could not be reached.</summary>
public class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
///     The calls to the payment provider are suspended because it failed many times in a row (circuit breaker). The
///     caller answers at once, without waiting, and can try again after <see cref="RetryAfter" />.
/// </summary>
public class PaymentGatewayUnavailableException(string message, TimeSpan retryAfter, Exception? innerException = null)
    : PaymentGatewayException(message, innerException)
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

/// <summary>The signature of a webhook is missing or does not match the payload.</summary>
public class InvalidWebhookSignatureException(string message, Exception? innerException = null)
    : Exception(message, innerException);
