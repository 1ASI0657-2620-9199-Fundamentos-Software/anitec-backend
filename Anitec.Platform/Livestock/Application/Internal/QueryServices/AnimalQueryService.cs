using Anitec.Platform.Clients.Application.QueryServices;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Livestock.Domain.Repositories;
using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Livestock.Application.Internal.QueryServices;

public class AnimalQueryService(
    IAnimalRepository repository,
    IHerdRepository herdRepository,
    IVeterinarianClientQueryService veterinarianClientQueryService) : IAnimalQueryService
{
    public async Task<Animal?> Handle(GetAnimalByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Animal>> Handle(GetAllAnimalsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Animal>> Handle(GetAnimalsForCallerQuery query, CancellationToken cancellationToken)
    {
        var visibleHerdIds = await GetVisibleHerdIdsAsync(query.Caller, cancellationToken);
        if (visibleHerdIds.Count == 0) return [];

        var animals = await repository.ListAsync(cancellationToken);
        return animals.Where(animal => visibleHerdIds.Contains(animal.HerdId)).ToList();
    }

    public async Task<Animal?> Handle(GetAnimalByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var animal = await repository.FindByIdAsync(query.Id, cancellationToken);
        if (animal is null) return null;

        var visibleHerdIds = await GetVisibleHerdIdsAsync(query.Caller, cancellationToken);
        return visibleHerdIds.Contains(animal.HerdId) ? animal : null;
    }

    public async Task<bool> Handle(IsHerdOwnedByCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher) return false;

        var herd = await herdRepository.FindByIdAsync(query.HerdId, cancellationToken);
        return herd is not null && herd.OwnerId == query.Caller.Id;
    }

    public async Task<bool> Handle(AreAnimalsOwnedByCallerQuery query, CancellationToken cancellationToken)
    {
        if (!query.Caller.IsRancher || query.AnimalIds.Count == 0) return false;

        var ownedHerdIds = await GetOwnedHerdIdsAsync(query.Caller, cancellationToken);
        var animals = await repository.ListAsync(cancellationToken);
        var ownedAnimalIds = animals
            .Where(animal => ownedHerdIds.Contains(animal.HerdId))
            .Select(animal => animal.Id)
            .ToHashSet();

        return query.AnimalIds.All(ownedAnimalIds.Contains);
    }

    private async Task<HashSet<int>> GetOwnedHerdIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        var herds = await herdRepository.ListAsync(cancellationToken);
        return herds.Where(herd => herd.OwnerId == caller.Id).Select(herd => herd.Id).ToHashSet();
    }

    /// <summary>
    ///     A rancher sees its own herds; a veterinarian sees the herds of the ranchers that accepted it.
    ///     Any other role sees nothing.
    /// </summary>
    private async Task<HashSet<int>> GetVisibleHerdIdsAsync(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.IsRancher) return await GetOwnedHerdIdsAsync(caller, cancellationToken);
        if (!caller.IsVeterinarian) return [];

        var acceptedRancherIds = await veterinarianClientQueryService.Handle(
            new GetAcceptedRancherIdsByVeterinarianIdQuery(caller.Id),
            cancellationToken);
        var herds = await herdRepository.ListAsync(cancellationToken);
        return herds.Where(herd => acceptedRancherIds.Contains(herd.OwnerId)).Select(herd => herd.Id).ToHashSet();
    }
}
