using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Devices.Application.Internal.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.Internal.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Metrics.Application.Internal.QueryServices;
using Anitec.Platform.Metrics.Domain.Model.Entities;
using Anitec.Platform.Metrics.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class ListingAuthorizationWorld
{
    public InMemoryHerdRepository Herds { get; } = new();
    public InMemoryCorralRepository Corrals { get; } = new();
    public InMemoryAnimalRepository Animals { get; } = new();
    public InMemoryDeviceRepository Devices { get; } = new();
    public InMemoryDeviceMetricRepository Metrics { get; } = new();
    public InMemoryVeterinarianClientRepository Relationships { get; } = new();
    public Dictionary<int, Herd> HerdByOwner { get; } = new();
    public List<string> LastNames { get; set; } = [];
    public bool? LastItemFound { get; set; }

    public HerdQueryService CreateHerdQueryService()
    {
        return new HerdQueryService(Herds, new VeterinarianClientQueryService(Relationships));
    }

    public AnimalQueryService CreateAnimalQueryService()
    {
        return new AnimalQueryService(Animals, Herds, new VeterinarianClientQueryService(Relationships));
    }

    public CorralQueryService CreateCorralQueryService()
    {
        return new CorralQueryService(Corrals, CreateHerdQueryService());
    }

    public DeviceQueryService CreateDeviceQueryService()
    {
        return new DeviceQueryService(Devices, CreateHerdQueryService(), CreateAnimalQueryService());
    }

    public DeviceMetricQueryService CreateMetricQueryService()
    {
        return new DeviceMetricQueryService(Metrics, CreateDeviceQueryService());
    }
}

[Binding]
public class ListingAuthorizationSteps(ListingAuthorizationWorld world)
{
    // Every resource is named after the herd it belongs to, so a result can be traced to its owner.
    [Given(@"the rancher ""(.*)"" with id (\d+) has the herd ""(.*)"" with a corral, a device and a reading")]
    public async Task GivenTheRancherHasAHerd(string rancherName, int rancherId, string herdName)
    {
        var herd = world.Herds.Seed(herdName, rancherId);
        world.HerdByOwner[rancherId] = herd;
        world.Corrals.Seed($"{herdName} corral", herd.Id);

        var device = new Device { Name = $"{herdName} device", HerdId = herd.Id };
        await world.Devices.AddAsync(device);
        await world.Metrics.AddAsync(new DeviceMetric { DeviceId = device.Id, Type = device.Name });
    }

    [Given(@"rancher (\d+) also has a device installed on one of its animals")]
    public async Task GivenARancherHasADeviceOnAnAnimal(int rancherId)
    {
        var herd = world.HerdByOwner[rancherId];
        var animal = new Animal { Tag = $"{herd.Name}-1", HerdId = herd.Id };
        await world.Animals.AddAsync(animal);
        await world.Devices.AddAsync(new Device { Name = $"{herd.Name} animal device", AnimalId = animal.Id });
    }

    [Given("a device that is not installed anywhere exists")]
    public async Task GivenADeviceWithoutTargetExists()
    {
        await world.Devices.AddAsync(new Device { Name = "Orphan device" });
    }

    [Given(@"veterinarian (\d+) has a ""(.*)"" link with rancher (\d+)")]
    public void GivenTheVeterinarianHasALink(int veterinarianId, string status, int rancherId)
    {
        world.Relationships.Seed(veterinarianId, rancherId, status);
    }

    [When(@"user (\d+) with role ""(.*)"" lists the (herds|corrals|devices|readings)")]
    public async Task WhenUserListsTheResources(int userId, string role, string resources)
    {
        var caller = new CallerContext(userId, role);
        world.LastNames = resources switch
        {
            "herds" => (await world.CreateHerdQueryService().Handle(new GetHerdsForCallerQuery(caller), CancellationToken.None))
                .Select(herd => herd.Name).ToList(),
            "corrals" => (await world.CreateCorralQueryService().Handle(new GetCorralsForCallerQuery(caller), CancellationToken.None))
                .Select(corral => corral.Name).ToList(),
            "devices" => (await world.CreateDeviceQueryService().Handle(new GetDevicesForCallerQuery(caller), CancellationToken.None))
                .Select(device => device.Name).ToList(),
            _ => (await world.CreateMetricQueryService().Handle(new GetDeviceMetricsForCallerQuery(caller), CancellationToken.None))
                .Select(metric => metric.Type).ToList()
        };
    }

    [When(@"user (\d+) with role ""(.*)"" asks for the (herd|corral|device|reading) of rancher (\d+)")]
    public async Task WhenUserAsksForAResource(int userId, string role, string resource, int ownerId)
    {
        var caller = new CallerContext(userId, role);
        var herdId = world.HerdByOwner[ownerId].Id;

        world.LastItemFound = resource switch
        {
            "herd" => await world.CreateHerdQueryService()
                .Handle(new GetHerdByIdForCallerQuery(herdId, caller), CancellationToken.None) is not null,
            "corral" => await world.CreateCorralQueryService().Handle(
                new GetCorralByIdForCallerQuery(world.Corrals.Items.First(item => item.HerdId == herdId).Id, caller),
                CancellationToken.None) is not null,
            "device" => await world.CreateDeviceQueryService().Handle(
                new GetDeviceByIdForCallerQuery(world.Devices.Items.First(item => item.HerdId == herdId).Id, caller),
                CancellationToken.None) is not null,
            _ => await world.CreateMetricQueryService().Handle(
                new GetDeviceMetricByIdForCallerQuery(
                    world.Metrics.Items.First(item => item.DeviceId == world.Devices.Items.First(device => device.HerdId == herdId).Id).Id,
                    caller),
                CancellationToken.None) is not null
        };
    }

    [Then(@"(\d+) results are returned")]
    public void ThenResultsAreReturned(int expected)
    {
        Assert.Equal(expected, world.LastNames.Count);
    }

    [Then(@"every result is related to ""(.*)""")]
    public void ThenEveryResultIsRelatedTo(string herdName)
    {
        Assert.NotEmpty(world.LastNames);
        Assert.All(world.LastNames, name => Assert.StartsWith(herdName, name));
    }

    [Then("the item is found")]
    public void ThenTheItemIsFound()
    {
        Assert.True(world.LastItemFound);
    }

    [Then("the item is not found")]
    public void ThenTheItemIsNotFound()
    {
        Assert.False(world.LastItemFound);
    }

    [Then(@"user (\d+) with role ""(.*)"" is the owner of the herd of rancher (\d+)")]
    public async Task ThenUserOwnsTheHerd(int userId, string role, int ownerId)
    {
        Assert.True(await IsOwnerAsync(userId, role, ownerId));
    }

    [Then(@"user (\d+) with role ""(.*)"" is not the owner of the herd of rancher (\d+)")]
    public async Task ThenUserDoesNotOwnTheHerd(int userId, string role, int ownerId)
    {
        Assert.False(await IsOwnerAsync(userId, role, ownerId));
    }

    private async Task<bool> IsOwnerAsync(int userId, string role, int ownerId)
    {
        return await world.CreateHerdQueryService().Handle(
            new IsHerdOwnedByCallerQuery(world.HerdByOwner[ownerId].Id, new CallerContext(userId, role)),
            CancellationToken.None);
    }
}
