using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;

namespace Anitec.Platform.Livestock.Application.QueryServices;

public interface IAnimalQueryService
{
    Task<Animal?> Handle(GetAnimalByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every animal without authorization filtering. Reserved for internal aggregations;
    ///     API endpoints must use <see cref="GetAnimalsForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<Animal>> Handle(GetAllAnimalsQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<Animal>> Handle(GetAnimalsForCallerQuery query, CancellationToken cancellationToken);
    Task<Animal?> Handle(GetAnimalByIdForCallerQuery query, CancellationToken cancellationToken);
    Task<bool> Handle(IsHerdOwnedByCallerQuery query, CancellationToken cancellationToken);
    Task<bool> Handle(AreAnimalsOwnedByCallerQuery query, CancellationToken cancellationToken);
}
