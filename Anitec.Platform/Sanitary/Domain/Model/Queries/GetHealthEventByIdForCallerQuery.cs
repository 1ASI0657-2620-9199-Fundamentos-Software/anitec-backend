using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Sanitary.Domain.Model.Queries;

public record GetHealthEventByIdForCallerQuery(int Id, CallerContext Caller);
