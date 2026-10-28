using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

/// <summary>Corrals that belong to the herds the caller may see.</summary>
public record GetCorralsForCallerQuery(CallerContext Caller);
