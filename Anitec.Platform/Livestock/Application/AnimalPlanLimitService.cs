using Anitec.Platform.Livestock.Application.Internal.OutboundServices;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Application;

public enum PlanLimitReason
{
    /// <summary>The rancher has no active plan: nothing limits it (as before the plans existed).</summary>
    NoPlan,

    /// <summary>The plan allows the animals that the rancher wants to add.</summary>
    WithinLimit,

    /// <summary>The animals would pass the limit of the plan.</summary>
    LimitReached,

    /// <summary>The Subscriptions service could not be asked. The rancher is allowed to continue (fail-open).</summary>
    PlanServiceUnavailable,

    /// <summary>The check does not apply: not a rancher, nothing to add, or no token or address to ask with.</summary>
    NotApplicable
}

/// <param name="Allowed">Whether the animals can be added.</param>
/// <param name="CurrentAnimals">How many animals the rancher has now (0 when it was not counted).</param>
public record PlanLimitDecision(
    bool Allowed,
    PlanLimitReason Reason,
    int CurrentAnimals = 0,
    int? MaxAnimals = null,
    string? PlanName = null);

public interface IAnimalPlanLimitService
{
    /// <summary>Decides whether the rancher can add <paramref name="additionalAnimals" /> to its herds.</summary>
    Task<PlanLimitDecision> CheckAsync(CallerContext caller, string? bearerToken, int additionalAnimals,
        CancellationToken cancellationToken);
}

/// <summary>
///     Applies the animal limit of the plan that the rancher paid for (the limit that the plans always advertised and
///     the core never enforced). The rule is chosen so that it can only add a restriction where a plan says so: without
///     an active plan, or when Subscriptions cannot be reached, the rancher keeps working.
/// </summary>
public class AnimalPlanLimitService(
    ISubscriptionPlanClient planClient,
    IAnimalQueryService animals,
    ILogger<AnimalPlanLimitService> logger) : IAnimalPlanLimitService
{
    public async Task<PlanLimitDecision> CheckAsync(CallerContext caller, string? bearerToken, int additionalAnimals,
        CancellationToken cancellationToken)
    {
        if (!caller.IsRancher || additionalAnimals < 1 || string.IsNullOrWhiteSpace(bearerToken))
            return new PlanLimitDecision(true, PlanLimitReason.NotApplicable);

        var lookup = await planClient.GetActivePlanAsync(bearerToken, cancellationToken);
        switch (lookup.Status)
        {
            case PlanLookupStatus.NoActivePlan:
                return new PlanLimitDecision(true, PlanLimitReason.NoPlan);
            case PlanLookupStatus.NotConfigured:
                return new PlanLimitDecision(true, PlanLimitReason.NotApplicable);
            case PlanLookupStatus.Unavailable:
                logger.LogWarning(
                    "The plan of rancher {RancherId} could not be checked: Subscriptions is not available, so the animals are allowed",
                    caller.Id);
                return new PlanLimitDecision(true, PlanLimitReason.PlanServiceUnavailable);
        }

        var plan = lookup.Plan!;
        var current = (await animals.Handle(new GetAnimalsForCallerQuery(caller), cancellationToken)).Count();
        return current + additionalAnimals > plan.MaxAnimals
            ? new PlanLimitDecision(false, PlanLimitReason.LimitReached, current, plan.MaxAnimals, plan.PlanName)
            : new PlanLimitDecision(true, PlanLimitReason.WithinLimit, current, plan.MaxAnimals, plan.PlanName);
    }
}
