using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Iam.Domain.Model.Queries;

public record GetUserByIdForCallerQuery(int Id, CallerContext Caller);
