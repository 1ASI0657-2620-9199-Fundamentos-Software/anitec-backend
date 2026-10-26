using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Devices.Domain.Repositories;
using Anitec.Platform.Iam.Application.Internal.OutboundServices;

namespace Anitec.Platform.Devices.Application.Internal.QueryServices;

public class DeviceQueryService(IDeviceRepository repository, IHashingService hashingService) : IDeviceQueryService
{
    public async Task<Device?> Handle(GetDeviceByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<Device>> Handle(GetAllDevicesQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    public async Task<Device?> Handle(AuthenticateDeviceQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ApiKey)) return null;

        var device = await repository.FindByIdAsync(query.DeviceId, cancellationToken);
        if (device?.ApiKeyHash is null) return null;

        return hashingService.VerifyPassword(query.ApiKey, device.ApiKeyHash) ? device : null;
    }
}




