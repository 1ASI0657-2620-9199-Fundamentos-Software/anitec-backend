using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Activities.Domain.Model.Queries;

/// <summary>
///     Activities the caller may see: those owned by a rancher caller, or those assigned to a veterinarian
///     caller or owned by a rancher that approved the veterinarian.
/// </summary>
public record GetFarmActivitiesForCallerQuery(CallerContext Caller);
