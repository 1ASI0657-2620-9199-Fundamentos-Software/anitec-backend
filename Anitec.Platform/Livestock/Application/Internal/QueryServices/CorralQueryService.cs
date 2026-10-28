using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Livestock.Domain.Repositories;

namespace Anitec.Platform.Livestock.Application.Internal.QueryServices;

public class CorralQueryService(ICorralRepository repository, IHerdQueryService herdQueryService) : ICorralQueryService
{
    public async Task<Corral?> Handle(GetCorralByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Corral>> Handle(GetAllCorralsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Corral>> Handle(GetCorralsForCallerQuery query, CancellationToken cancellationToken)
    {
        var herds = await herdQueryService.Handle(new GetHerdsForCallerQuery(query.Caller), cancellationToken);
        var visibleHerdIds = herds.Select(herd => herd.Id).ToHashSet();
        if (visibleHerdIds.Count == 0) return [];

        var corrals = await repository.ListAsync(cancellationToken);
        return corrals.Where(corral => visibleHerdIds.Contains(corral.HerdId)).ToList();
    }

    public async Task<Corral?> Handle(GetCorralByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var corral = await repository.FindByIdAsync(query.Id, cancellationToken);
        if (corral is null) return null;

        var herd = await herdQueryService.Handle(new GetHerdByIdForCallerQuery(corral.HerdId, query.Caller), cancellationToken);
        return herd is null ? null : corral;
    }
}
