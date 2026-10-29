using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Clients.Domain.Model.Queries;

/// <summary>
///     Ids of the users that have an open relationship (Pending or Accepted) with the caller: the ranchers
///     of a veterinarian or the veterinarians of a rancher.
/// </summary>
public record GetRelatedUserIdsQuery(CallerContext Caller);
