using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Domain.Model.Queries;

/// <summary>
///     True only when every listed animal exists and belongs to a herd owned by the caller (a rancher).
/// </summary>
public record AreAnimalsOwnedByCallerQuery(IReadOnlyCollection<int> AnimalIds, CallerContext Caller);
