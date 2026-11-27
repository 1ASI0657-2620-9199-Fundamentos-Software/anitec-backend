using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Anitec.Platform.Livestock.Application.Internal.OutboundServices;
using Anitec.Platform.Shared.Application.AuditLogging;
using Anitec.Platform.Shared.Infrastructure.Pipeline.Middleware.Components;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Anitec.Platform.Livestock.Infrastructure.Subscriptions;

/// <summary>
///     Settings of the call to the Subscriptions service. They come from <c>Subscriptions__*</c> variables. Without
///     <see cref="BaseUrl" /> the limit of animals is simply not checked.
/// </summary>
public class SubscriptionsClientOptions
{
    public const string SectionName = "Subscriptions";

    /// <summary>Address of the service, for example http://subscriptions:8080. Empty turns the check off.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The whole call, with its retries, may not take longer than this: a rancher must not wait for payments.</summary>
    public int TimeoutSeconds { get; set; } = 2;

    public double FailureRatio { get; set; } = 0.5;
    public int MinimumThroughput { get; set; } = 3;
    public int SamplingDurationSeconds { get; set; } = 30;
    public int BreakDurationSeconds { get; set; } = 30;
}

public static class SubscriptionsClientRegistration
{
    public const string HttpClientName = "subscriptions";

    /// <summary>
    ///     Timeout and circuit breaker of the call (iteration 7, availability tactic). When Subscriptions fails
    ///     repeatedly the circuit opens and the next calls fail at once instead of waiting; the client turns that into
    ///     "unavailable" and the rancher continues.
    /// </summary>
    public static IServiceCollection AddSubscriptionPlanClient(this IServiceCollection services)
    {
        services.AddHttpClient<ISubscriptionPlanClient, SubscriptionPlanClient>(HttpClientName)
            .AddResilienceHandler("subscriptions", (builder, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<SubscriptionsClientOptions>>().Value;
                // The breaker is added first, so it is the outer strategy and counts the calls that the timeout cuts.
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = options.FailureRatio,
                    MinimumThroughput = options.MinimumThroughput,
                    SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
                    BreakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds)
                });
                builder.AddTimeout(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
            });
        return services;
    }
}

public class SubscriptionPlanClient(
    HttpClient httpClient,
    IOptions<SubscriptionsClientOptions> options,
    ICorrelationIdAccessor correlation,
    ILogger<SubscriptionPlanClient> logger) : ISubscriptionPlanClient
{
    public async Task<PlanLookupResult> GetActivePlanAsync(string bearerToken, CancellationToken cancellationToken)
    {
        var baseUrl = options.Value.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl)) return PlanLookupResult.NotConfigured;

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/api/v1/subscriptions/active-plan");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        // The same id travels to the other service, so one request can be followed across both (QAS-10).
        if (correlation.CorrelationId is { } id)
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var plan = await response.Content.ReadFromJsonAsync<ActivePlanBody>(cancellationToken);
                    return plan is null || plan.MaxAnimals < 0
                        ? PlanLookupResult.Unavailable
                        : new PlanLookupResult(PlanLookupStatus.Found,
                            new ActivePlanInfo(plan.PlanId, plan.PlanName ?? string.Empty, plan.MaxAnimals));
                case HttpStatusCode.NotFound:
                    return PlanLookupResult.NoActivePlan;
                default:
                    logger.LogWarning("Subscriptions answered {StatusCode} when the plan was requested",
                        (int)response.StatusCode);
                    return PlanLookupResult.Unavailable;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is BrokenCircuitException or TimeoutRejectedException
                                              or HttpRequestException or OperationCanceledException
                                              or System.Text.Json.JsonException)
        {
            // BrokenCircuitException: the circuit is open, so the call did not even leave the process.
            logger.LogWarning("The plan could not be requested from Subscriptions ({Reason})", exception.GetType().Name);
            return PlanLookupResult.Unavailable;
        }
    }

    private sealed record ActivePlanBody(int PlanId, string? PlanName, int MaxAnimals);
}
