namespace Anitec.Platform.Livestock.Application.Internal.OutboundServices;

public enum PlanLookupStatus
{
    /// <summary>The rancher has an active subscription and its plan is known.</summary>
    Found,

    /// <summary>The Subscriptions service answered that the rancher has no active subscription.</summary>
    NoActivePlan,

    /// <summary>The service is not configured in this environment (no address).</summary>
    NotConfigured,

    /// <summary>The service did not answer in time, is down, or its circuit breaker is open.</summary>
    Unavailable
}

public record ActivePlanInfo(int PlanId, string PlanName, int MaxAnimals);

public record PlanLookupResult(PlanLookupStatus Status, ActivePlanInfo? Plan = null)
{
    public static readonly PlanLookupResult NoActivePlan = new(PlanLookupStatus.NoActivePlan);
    public static readonly PlanLookupResult NotConfigured = new(PlanLookupStatus.NotConfigured);
    public static readonly PlanLookupResult Unavailable = new(PlanLookupStatus.Unavailable);
}

/// <summary>
///     Outbound port to the Subscriptions service, the first call from the core to another service. It never throws
///     for a failure of the service: it answers <see cref="PlanLookupStatus.Unavailable" />, so a failure of payments
///     cannot stop a rancher from working (QAS-09).
/// </summary>
public interface ISubscriptionPlanClient
{
    /// <param name="bearerToken">The token of the rancher, forwarded as received; the service reads the identity from it.</param>
    Task<PlanLookupResult> GetActivePlanAsync(string bearerToken, CancellationToken cancellationToken);
}
