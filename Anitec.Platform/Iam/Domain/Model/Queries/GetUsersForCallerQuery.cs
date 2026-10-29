using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Iam.Domain.Model.Queries;

/// <summary>
///     Users the caller may see: itself and the users that have an open relationship with it.
/// </summary>
public record GetUsersForCallerQuery(CallerContext Caller);
