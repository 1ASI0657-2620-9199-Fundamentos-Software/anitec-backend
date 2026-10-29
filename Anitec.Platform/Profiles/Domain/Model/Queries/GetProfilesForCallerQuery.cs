using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Profiles.Domain.Model.Queries;

/// <summary>Profiles created by the caller. A profile without an owner is visible to nobody.</summary>
public record GetProfilesForCallerQuery(CallerContext Caller);
