using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

public record IsHerdOwnedByCallerQuery(int HerdId, CallerContext Caller);
