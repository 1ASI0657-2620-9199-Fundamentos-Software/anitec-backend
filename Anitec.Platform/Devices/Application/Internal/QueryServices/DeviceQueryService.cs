using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Devices.Domain.Repositories;
using Anitec.Platform.Livestock.Application.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Queries;

namespace Anitec.Platform.Devices.Application.Internal.QueryServices;

public class DeviceQueryService(
    IDeviceRepository repository,
    IHerdQueryService herdQueryService,
    IAnimalQueryService animalQueryService) : IDeviceQueryService
{
    public async Task<Device?> Handle(GetDeviceByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Device>> Handle(GetAllDevicesQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    /// <summary>
    ///     A device is visible when the herd or the animal it is installed on is visible to the caller. A device
    ///     installed on neither is visible to nobody.
    /// </summary>
    public async Task<IEnumerable<Device>> Handle(GetDevicesForCallerQuery query, CancellationToken cancellationToken)
    {
        var herds = await herdQueryService.Handle(new GetHerdsForCallerQuery(query.Caller), cancellationToken);
        var animals = await animalQueryService.Handle(new GetAnimalsForCallerQuery(query.Caller), cancellationToken);
        var herdIds = herds.Select(herd => herd.Id).ToHashSet();
        var animalIds = animals.Select(animal => animal.Id).ToHashSet();

        var devices = await repository.ListAsync(cancellationToken);
        return devices
            .Where(device => (device.HerdId is { } herdId && herdIds.Contains(herdId))
                             || (device.AnimalId is { } animalId && animalIds.Contains(animalId)))
            .ToList();
    }

    public async Task<Device?> Handle(GetDeviceByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var devices = await Handle(new GetDevicesForCallerQuery(query.Caller), cancellationToken);
        return devices.FirstOrDefault(device => device.Id == query.Id);
    }

    public async Task<Device?> Handle(AuthenticateDeviceQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ApiKey)) return null;

        var device = await repository.FindByIdAsync(query.DeviceId, cancellationToken);
        if (device?.ApiKeyHash is null) return null;

        return DeviceApiKeyHasher.Matches(query.ApiKey, device.ApiKeyHash) ? device : null;
    }
}
