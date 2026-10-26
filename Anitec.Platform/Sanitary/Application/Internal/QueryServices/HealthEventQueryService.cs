using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Sanitary.Application.QueryServices;
using Anitec.Platform.Sanitary.Domain.Model.Entities;
using Anitec.Platform.Sanitary.Domain.Model.Queries;
using Anitec.Platform.Sanitary.Domain.Repositories;

namespace Anitec.Platform.Sanitary.Application.Internal.QueryServices;

public class HealthEventQueryService(
    IHealthEventRepository repository,
    IAnimalQueryService animalQueryService) : IHealthEventQueryService
{
    public async Task<HealthEvent?> Handle(GetHealthEventByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<HealthEvent>> Handle(GetAllHealthEventsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<IEnumerable<HealthEvent>> Handle(GetHealthEventsForCallerQuery query, CancellationToken cancellationToken)
    {
        var visibleAnimals = await animalQueryService.Handle(new GetAnimalsForCallerQuery(query.Caller), cancellationToken);
        var visibleAnimalIds = visibleAnimals.Select(animal => animal.Id).ToHashSet();
        if (visibleAnimalIds.Count == 0) return [];

        var events = await repository.ListAsync(cancellationToken);
        return events.Where(item => visibleAnimalIds.Contains(item.AnimalId)).ToList();
    }

    public async Task<HealthEvent?> Handle(GetHealthEventByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var healthEvent = await repository.FindByIdAsync(query.Id, cancellationToken);
        if (healthEvent is null) return null;

        var animal = await animalQueryService.Handle(
            new GetAnimalByIdForCallerQuery(healthEvent.AnimalId, query.Caller),
            cancellationToken);
        return animal is null ? null : healthEvent;
    }
}
