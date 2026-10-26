using Anitec.Platform.Metrics.Application.CommandServices;
using Anitec.Platform.Metrics.Domain.Model.Commands;
using Anitec.Platform.Metrics.Domain.Model;
using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Metrics.Domain.Repositories;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Metrics.Application.Internal.CommandServices;

public class DeviceMetricCommandService(IDeviceMetricRepository repository, IUnitOfWork unitOfWork)
    : IDeviceMetricCommandService
{
    public async Task<Result<DeviceMetric>> Handle(CreateDeviceMetricCommand command, CancellationToken cancellationToken)
    {
        var entity = new DeviceMetric(command);
        await repository.AddAsync(entity, cancellationToken);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<DeviceMetric>.Success(entity);
    }

    public async Task<Result<DeviceMetric>> Handle(IngestDeviceReadingCommand command, CancellationToken cancellationToken)
    {
        // The interval is checked against the last stored reading, not against memory of this process, so it
        // holds no matter which replica receives the request.
        var latest = await repository.FindLatestByDeviceIdAsync(command.DeviceId, cancellationToken);
        if (latest is not null && command.Now - latest.RecordedAt < command.MinInterval)
            return Result<DeviceMetric>.Failure(MetricsError.ReadingTooSoon, "The device already sent a reading in the last interval.");

        return await Handle(
            new CreateDeviceMetricCommand(command.DeviceId, command.Type, command.Value, command.Unit, command.Now),
            cancellationToken);
    }

    public async Task<Result<DeviceMetric>> Handle(UpdateDeviceMetricCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result<DeviceMetric>.Failure(MetricsError.DeviceMetricNotFound, "DeviceMetric not found.");
        entity.DeviceId = command.DeviceId;
        entity.Type = command.Type;
        entity.Value = command.Value;
        entity.Unit = command.Unit;
        entity.RecordedAt = command.RecordedAt;

        repository.Update(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result<DeviceMetric>.Success(entity);
    }

    public async Task<Result> Handle(DeleteDeviceMetricCommand command, CancellationToken cancellationToken)
    {
        var entity = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (entity is null) return Result.Failure(MetricsError.DeviceMetricNotFound, "DeviceMetric not found.");
        repository.Remove(entity);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result.Success();
    }
}
