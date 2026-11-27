using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Stripe;

namespace Anitec.Subscriptions.Infrastructure.Payments.Stripe;

/// <summary>
///     Limits of the calls to Stripe (iteration 7, availability tactic). They come from <c>Resilience__Stripe__*</c>.
/// </summary>
public class StripeResilienceOptions
{
    public const string SectionName = "Resilience:Stripe";

    /// <summary>A call that takes longer than this is cut and counts as a failure.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>The circuit opens when this share of the calls of the sampling window failed.</summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>The circuit never opens with fewer calls than this in the window.</summary>
    public int MinimumThroughput { get; set; } = 3;

    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>How long the circuit stays open before it lets one call through to see if Stripe is back.</summary>
    public int BreakDurationSeconds { get; set; } = 30;
}

/// <summary>
///     Builds the pipeline that protects the service from a prolonged failure of Stripe (4.3.7.4): a timeout per call
///     and a circuit breaker. While the circuit is open the calls fail at once instead of waiting for an answer that
///     will not arrive, so the requests of the users do not pile up. There is no automatic retry: creating a checkout
///     session twice would open two payment pages.
/// </summary>
public static class StripeResilience
{
    public static ResiliencePipeline Create(StripeResilienceOptions options, TimeProvider? clock = null)
    {
        var builder = new ResiliencePipelineBuilder { TimeProvider = clock ?? TimeProvider.System };

        // The breaker is added first, so it is the outer strategy: a call that the timeout cuts is a failure for it.
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = options.FailureRatio,
            MinimumThroughput = options.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds),
            ShouldHandle = new PredicateBuilder()
                .Handle<StripeException>(IsProviderFailure)
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
        });
        builder.AddTimeout(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
        return builder.Build();
    }

    /// <summary>
    ///     Only the failures of the provider count: no answer, 429 and 5xx. A card that is refused or a session that
    ///     does not exist is the user's case, not an outage.
    /// </summary>
    private static bool IsProviderFailure(StripeException exception)
    {
        var status = exception.HttpStatusCode;
        return status == 0 || status == HttpStatusCode.TooManyRequests || (int)status >= 500;
    }
}
