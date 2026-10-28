using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

public record GetCorralByIdForCallerQuery(int Id, CallerContext Caller);
