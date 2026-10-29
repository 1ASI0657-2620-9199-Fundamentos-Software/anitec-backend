using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Profiles.Domain.Model.Queries;

public record GetProfileByIdForCallerQuery(int ProfileId, CallerContext Caller);
