using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Metrics.Domain.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Platform.Metrics.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class DeviceMetricRepository(AppDbContext context) : BaseRepository<DeviceMetric>(context), IDeviceMetricRepository
{
    public async Task<DeviceMetric?> FindLatestByDeviceIdAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<DeviceMetric>()
            .Where(metric => metric.DeviceId == deviceId)
            .OrderByDescending(metric => metric.RecordedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
