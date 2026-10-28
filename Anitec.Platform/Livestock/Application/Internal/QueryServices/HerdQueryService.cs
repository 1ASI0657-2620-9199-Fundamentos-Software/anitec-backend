using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Livestock.Domain.Repositories;

namespace Anitec.Platform.Livestock.Application.Internal.QueryServices;

public class HerdQueryService(
    IHerdRepository repository,
    IVeterinarianClientQueryService veterinarianClientQueryService) : IHerdQueryService
{
    public async Task<Herd?> Handle(GetHerdByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Herd>> Handle(GetAllHerdsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    /// <summary>
    ///     A rancher sees its own herds; a veterinarian sees the herds of the ranchers that accepted it.
    ///     Any other role sees nothing.
    /// </summary>
    public async Task<IEnumerable<Herd>> Handle(GetHerdsForCallerQuery query, CancellationToken cancellationToken)
    {
        var caller = query.Caller;
        var herds = await repository.ListAsync(cancellationToken);

        if (caller.IsRancher) return herds.Where(herd => herd.OwnerId == caller.Id).ToList();
        if (!caller.IsVeterinarian) return [];

        var acceptedRancherIds = await veterinarianClientQueryService.Handle(
            new GetAcceptedRancherIdsByVeterinarianIdQuery(caller.Id),
            cancellationToken);
        return herds.Where(herd => acceptedRancherIds.Contains(herd.OwnerId)).ToList();
    }

    public async Task<Herd?> Handle(GetHerdByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var herds = await Handle(new GetHerdsForCallerQuery(query.Caller), cancellationToken);
        return herds.FirstOrDefault(herd => herd.Id == query.Id);
    }

    public async Task<bool> Handle(IsHerdOwnedByCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return false;

        var herd = await repository.FindByIdAsync(query.HerdId, cancellationToken);
        return herd is not null && herd.OwnerId == query.Caller.Id;
    }
}
