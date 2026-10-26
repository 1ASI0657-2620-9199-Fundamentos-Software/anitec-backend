using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Activities.Domain.Model.Queries;

public record GetFarmActivityByIdForCallerQuery(int Id, CallerContext Caller);
