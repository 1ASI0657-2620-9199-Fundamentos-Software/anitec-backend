using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;

namespace Anitec.Platform.Livestock.Application.QueryServices;

public interface IHerdQueryService
{
    Task<Herd?> Handle(GetHerdByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every herd without authorization filtering. Reserved for internal aggregations that
    ///     apply their own rule; API endpoints must use <see cref="GetHerdsForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<Herd>> Handle(GetAllHerdsQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<Herd>> Handle(GetHerdsForCallerQuery query, CancellationToken cancellationToken);
    Task<Herd?> Handle(GetHerdByIdForCallerQuery query, CancellationToken cancellationToken);

    /// <summary>True when the herd exists and belongs to the caller, who must be a rancher.</summary>
    Task<bool> Handle(IsHerdOwnedByCallerQuery query, CancellationToken cancellationToken);
}
