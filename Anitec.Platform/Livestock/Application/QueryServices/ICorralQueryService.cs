using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;

namespace Anitec.Platform.Livestock.Application.QueryServices;

public interface ICorralQueryService
{
    Task<Corral?> Handle(GetCorralByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every corral without authorization filtering. API endpoints must use
    ///     <see cref="GetCorralsForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<Corral>> Handle(GetAllCorralsQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<Corral>> Handle(GetCorralsForCallerQuery query, CancellationToken cancellationToken);
    Task<Corral?> Handle(GetCorralByIdForCallerQuery query, CancellationToken cancellationToken);
}
