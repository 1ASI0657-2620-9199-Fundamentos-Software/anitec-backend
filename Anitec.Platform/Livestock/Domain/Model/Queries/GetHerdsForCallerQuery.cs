using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

/// <summary>
///     Herds the caller may see: a rancher sees its own herds and a veterinarian sees the herds of the
///     ranchers that approved the veterinarian.
/// </summary>
public record GetHerdsForCallerQuery(CallerContext Caller);
