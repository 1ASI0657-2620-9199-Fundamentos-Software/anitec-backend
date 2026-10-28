using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Metrics.Domain.Model.Queries;

namespace Anitec.Platform.Metrics.Application.QueryServices;

public interface IDeviceMetricQueryService
{
    Task<DeviceMetric?> Handle(GetDeviceMetricByIdQuery query, CancellationToken cancellationToken);

    /// <summary>
    ///     Returns every reading without authorization filtering. Reserved for internal aggregations that
    ///     apply their own rule; API endpoints must use <see cref="GetDeviceMetricsForCallerQuery" />.
    /// </summary>
    Task<IEnumerable<DeviceMetric>> Handle(GetAllDeviceMetricsQuery query, CancellationToken cancellationToken);

    Task<IEnumerable<DeviceMetric>> Handle(GetDeviceMetricsForCallerQuery query, CancellationToken cancellationToken);
    Task<DeviceMetric?> Handle(GetDeviceMetricByIdForCallerQuery query, CancellationToken cancellationToken);
}
