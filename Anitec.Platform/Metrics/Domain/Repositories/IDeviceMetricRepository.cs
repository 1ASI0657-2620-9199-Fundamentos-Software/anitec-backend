using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Metrics.Domain.Repositories;

public interface IDeviceMetricRepository : IBaseRepository<DeviceMetric>
{
    Task<DeviceMetric?> FindLatestByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default);
}
