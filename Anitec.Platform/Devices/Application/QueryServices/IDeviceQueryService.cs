using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Domain.Model.Queries;

namespace Anitec.Platform.Devices.Application.QueryServices;

public interface IDeviceQueryService
{
    Task<Device?> Handle(GetDeviceByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every device without authorization filtering. Reserved for internal aggregations that
    ///     apply their own rule; API endpoints must use <see cref="GetDevicesForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<Device>> Handle(GetAllDevicesQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<Device>> Handle(GetDevicesForCallerQuery query, CancellationToken cancellationToken);
    Task<Device?> Handle(GetDeviceByIdForCallerQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns the device when the API key matches the one issued for it, otherwise <c>null</c>.
    /// </summary>
    Task<Device?> Handle(AuthenticateDeviceQuery query, CancellationToken cancellationToken);
}
