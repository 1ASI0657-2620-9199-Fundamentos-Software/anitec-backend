using Anitec.Platform.Metrics.Application.Internal.CommandServices;
using Anitec.Platform.Metrics.Domain.Model;
using Anitec.Platform.Metrics.Domain.Model.Commands;
using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class DeviceReadingIntervalWorld
{
    public InMemoryDeviceMetricRepository Metrics { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public TimeSpan MinInterval { get; set; }
    public Result<DeviceMetric>? LastResult { get; set; }
}

[Binding]
public class DeviceReadingIntervalSteps(DeviceReadingIntervalWorld world)
{
    private static readonly DateTime Day = new(2026, 10, 26);

    private DeviceMetricCommandService CreateService()
    {
        return new DeviceMetricCommandService(world.Metrics, world.UnitOfWork);
    }

    private static DateTime At(string time)
    {
        return Day + TimeSpan.Parse(time);
    }

    [Given(@"the minimum interval between readings is (\d+) seconds")]
    public void GivenTheMinimumInterval(int seconds)
    {
        world.MinInterval = TimeSpan.FromSeconds(seconds);
    }

    [Given(@"a device with id (\d+) sent a reading of ([\d.]+) at (\d\d:\d\d:\d\d)")]
    public async Task GivenADeviceSentAReading(int deviceId, decimal value, string time)
    {
        var result = await CreateService().Handle(
            new IngestDeviceReadingCommand(deviceId, "Temperatura", value, "C", At(time), world.MinInterval),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [When(@"the device with id (\d+) sends a reading of ([\d.]+) at (\d\d:\d\d:\d\d)")]
    public async Task WhenTheDeviceSendsAReading(int deviceId, decimal value, string time)
    {
        world.LastResult = await CreateService().Handle(
            new IngestDeviceReadingCommand(deviceId, "Temperatura", value, "C", At(time), world.MinInterval),
            CancellationToken.None);
    }

    [Then("the reading is rejected because it came too soon")]
    public void ThenTheReadingIsRejected()
    {
        Assert.True(world.LastResult!.IsFailure);
        Assert.Equal(MetricsError.ReadingTooSoon, world.LastResult.Error);
    }

    [Then("the reading is stored")]
    public void ThenTheReadingIsStored()
    {
        Assert.True(world.LastResult!.IsSuccess);
    }

    [Then(@"the device with id (\d+) has (\d+) stored readings")]
    public void ThenTheDeviceHasStoredReadings(int deviceId, int count)
    {
        Assert.Equal(count, world.Metrics.Items.Count(m => m.DeviceId == deviceId));
    }
}
