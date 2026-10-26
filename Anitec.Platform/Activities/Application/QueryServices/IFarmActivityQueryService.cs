using Anitec.Platform.Activities.Domain.Model.Entities;
using Anitec.Platform.Activities.Domain.Model.Queries;

namespace Anitec.Platform.Activities.Application.QueryServices;

public interface IFarmActivityQueryService
{
    Task<FarmActivity?> Handle(GetFarmActivityByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every activity without authorization filtering. Reserved for internal use;
    ///     API endpoints must use <see cref="GetFarmActivitiesForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<FarmActivity>> Handle(GetAllFarmActivitiesQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<FarmActivity>> Handle(GetFarmActivitiesForCallerQuery query, CancellationToken cancellationToken);
    Task<FarmActivity?> Handle(GetFarmActivityByIdForCallerQuery query, CancellationToken cancellationToken);
}
