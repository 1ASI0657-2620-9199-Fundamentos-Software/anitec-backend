using Anitec.Platform.Devices.Application.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Metrics.Application.QueryServices;
using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Metrics.Domain.Model.Queries;
using Anitec.Platform.Metrics.Domain.Repositories;

namespace Anitec.Platform.Metrics.Application.Internal.QueryServices;

public class DeviceMetricQueryService(
    IDeviceMetricRepository repository,
    IDeviceQueryService deviceQueryService) : IDeviceMetricQueryService
{
    public async Task<DeviceMetric?> Handle(GetDeviceMetricByIdQuery query, CancellationToken cancellationToken)
    {
        return await repository.FindByIdAsync(query.Id, cancellationToken);
    }

    public async Task<IEnumerable<DeviceMetric>> Handle(GetAllDeviceMetricsQuery query, CancellationToken cancellationToken)
    {
        return await repository.ListAsync(cancellationToken);
    }

    /// <summary>A reading is visible when the device that sent it is visible to the caller.</summary>
    public async Task<IEnumerable<DeviceMetric>> Handle(GetDeviceMetricsForCallerQuery query, CancellationToken cancellationToken)
    {
        var devices = await deviceQueryService.Handle(new GetDevicesForCallerQuery(query.Caller), cancellationToken);
        var visibleDeviceIds = devices.Select(device => device.Id).ToHashSet();
        if (visibleDeviceIds.Count == 0) return [];

        var metrics = await repository.ListAsync(cancellationToken);
        return metrics.Where(metric => visibleDeviceIds.Contains(metric.DeviceId)).ToList();
    }

    public async Task<DeviceMetric?> Handle(GetDeviceMetricByIdForCallerQuery query, CancellationToken cancellationToken)
    {
        var metric = await repository.FindByIdAsync(query.Id, cancellationToken);
        if (metric is null) return null;

        var device = await deviceQueryService.Handle(new GetDeviceByIdForCallerQuery(metric.DeviceId, query.Caller), cancellationToken);
        return device is null ? null : metric;
    }
}
