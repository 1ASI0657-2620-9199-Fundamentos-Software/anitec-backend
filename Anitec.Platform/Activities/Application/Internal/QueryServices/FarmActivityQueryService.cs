using Anitec.Platform.Activities.Application.QueryServices;
using Anitec.Platform.Activities.Domain.Model.Entities;
using Anitec.Platform.Activities.Domain.Model.Queries;
using Anitec.Platform.Activities.Domain.Repositories;
using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Activities.Application.Internal.QueryServices;

public class FarmActivityQueryService(
    IFarmActivityRepository repository,
    IVeterinarianClientQueryService veterinarianClientQueryService) : IFarmActivityQueryService
{
    public async Task<FarmActivity?> Handle(GetFarmActivityByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<FarmActivity>> Handle(GetAllFarmActivitiesQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FarmActivity>> Handle(GetFarmActivitiesForCallerQuery query, CancellationToken cancellationToken)
    {
        var acceptedRancherIds = await GetAcceptedRancherIdsAsync(query.Caller, cancellationToken);
        var activities = await repository.ListAsync(cancellationToken);
        return activities.Where(activity => IsVisible(activity, query.Caller, acceptedRancherIds)).ToList();
    }

    public async Task<FarmActivity?> Handle(GetFarmActivityByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var activity = await repository.FindByIdAsync(query.Id, cancellationToken);
        if (activity is null) return null;

        var acceptedRancherIds = await GetAcceptedRancherIdsAsync(query.Caller, cancellationToken);
        return IsVisible(activity, query.Caller, acceptedRancherIds) ? activity : null;
    }

    private async Task<IReadOnlySet<int>> GetAcceptedRancherIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        if (!caller.IsVeterinarian) return new HashSet<int>();

        return await veterinarianClientQueryService.Handle(
            new GetAcceptedRancherIdsByVeterinarianIdQuery(caller.Id),
            cancellationToken);
    }

    private static bool IsVisible(FarmActivity activity, CallerContext caller, IReadOnlySet<int> acceptedRancherIds)
    {
        if (caller.IsRancher) return activity.OwnerId == caller.Id;
        if (!caller.IsVeterinarian) return false;

        return activity.VeterinarianId == caller.Id
               || activity.OwnerId.HasValue && acceptedRancherIds.Contains(activity.OwnerId.Value);
    }
}
