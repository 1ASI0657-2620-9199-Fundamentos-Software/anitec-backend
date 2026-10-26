using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

/// <summary>
///     Animals the caller may see: a rancher sees the animals of its own herds and a veterinarian
///     sees the animals of the ranchers that approved the veterinarian.
/// </summary>
public record GetAnimalsForCallerQuery(CallerContext Caller);
