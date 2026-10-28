using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

public record GetHerdByIdForCallerQuery(int Id, CallerContext Caller);
