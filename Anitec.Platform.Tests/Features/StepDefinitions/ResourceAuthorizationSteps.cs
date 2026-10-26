using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Financial.Application.Internal.QueryServices;
using Anitec.Platform.Financial.Domain.Model.Entities;
using Anitec.Platform.Financial.Domain.Model.Queries;
using Anitec.Platform.Livestock.Application.Internal.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Sanitary.Application.Internal.QueryServices;
using Anitec.Platform.Sanitary.Domain.Model.Entities;
using Anitec.Platform.Sanitary.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class ResourceAuthorizationWorld
{
    public InMemoryAnimalRepository Animals { get; } = new();
    public InMemoryHerdRepository Herds { get; } = new();
    public InMemoryHealthEventRepository HealthEvents { get; } = new();
    public InMemoryFinancialRecordRepository FinancialRecords { get; } = new();
    public InMemoryVeterinarianClientRepository Relationships { get; } = new();
    public Dictionary<int, Herd> HerdByOwner { get; } = new();
    public List<Animal> LastAnimals { get; set; } = [];
    public Animal? LastAnimal { get; set; }

    public AnimalQueryService CreateAnimalQueryService()
    {
        return new AnimalQueryService(Animals, Herds, new VeterinarianClientQueryService(Relationships));
    }

    public HealthEventQueryService CreateHealthEventQueryService()
    {
        return new HealthEventQueryService(HealthEvents, CreateAnimalQueryService());
    }

    public FinancialRecordQueryService CreateFinancialQueryService()
    {
        return new FinancialRecordQueryService(FinancialRecords);
    }
}

[Binding]
public class ResourceAuthorizationSteps(ResourceAuthorizationWorld world)
{
    [Given(@"the rancher ""(.*)"" with id (\d+) owns the herd ""(.*)"" with (\d+) animals")]
    public async Task GivenTheRancherOwnsAHerd(string rancherName, int rancherId, string herdName, int animals)
    {
        var herd = world.Herds.Seed(herdName, rancherId);
        world.HerdByOwner[rancherId] = herd;

        for (var index = 1; index <= animals; index++)
            await world.Animals.AddAsync(new Animal { Tag = $"{herdName}-{index}", HerdId = herd.Id });
    }

    [Given(@"veterinarian (\d+) has a ""(.*)"" relationship with rancher (\d+)")]
    public void GivenTheVeterinarianHasARelationship(int veterinarianId, string status, int rancherId)
    {
        world.Relationships.Seed(veterinarianId, rancherId, status);
    }

    [Given(@"a health event exists for an animal of rancher (\d+)")]
    public async Task GivenAHealthEventExists(int rancherId)
    {
        var herdId = world.HerdByOwner[rancherId].Id;
        var animal = world.Animals.Items.First(item => item.HerdId == herdId);
        await world.HealthEvents.AddAsync(new HealthEvent { AnimalId = animal.Id, Type = "Vaccination" });
    }

    [Given(@"a financial record exists for rancher (\d+)")]
    public async Task GivenAFinancialRecordExists(int rancherId)
    {
        await world.FinancialRecords.AddAsync(new FinancialRecord { OwnerId = rancherId, Type = "Income" });
    }

    [When(@"user (\d+) with role ""(.*)"" lists the animals")]
    public async Task WhenUserListsTheAnimals(int userId, string role)
    {
        var animals = await world.CreateAnimalQueryService()
            .Handle(new GetAnimalsForCallerQuery(new CallerContext(userId, role)), CancellationToken.None);
        world.LastAnimals = animals.ToList();
    }

    [When(@"user (\d+) with role ""(.*)"" asks for an animal of rancher (\d+)")]
    public async Task WhenUserAsksForAnAnimalOfRancher(int userId, string role, int ownerId)
    {
        var herdId = world.HerdByOwner[ownerId].Id;
        var animal = world.Animals.Items.First(item => item.HerdId == herdId);
        world.LastAnimal = await world.CreateAnimalQueryService()
            .Handle(new GetAnimalByIdForCallerQuery(animal.Id, new CallerContext(userId, role)), CancellationToken.None);
    }

    [Then(@"(\d+) animals are returned")]
    public void ThenAnimalsAreReturned(int expected)
    {
        Assert.Equal(expected, world.LastAnimals.Count);
    }

    [Then("the animal is not found")]
    public void ThenTheAnimalIsNotFound()
    {
        Assert.Null(world.LastAnimal);
    }

    [Then(@"user (\d+) with role ""(.*)"" sees (\d+) health events")]
    public async Task ThenUserSeesHealthEvents(int userId, string role, int expected)
    {
        var events = await world.CreateHealthEventQueryService()
            .Handle(new GetHealthEventsForCallerQuery(new CallerContext(userId, role)), CancellationToken.None);
        Assert.Equal(expected, events.Count());
    }

    [Then(@"user (\d+) with role ""(.*)"" sees (\d+) financial records")]
    public async Task ThenUserSeesFinancialRecords(int userId, string role, int expected)
    {
        var records = await world.CreateFinancialQueryService()
            .Handle(new GetFinancialRecordsForCallerQuery(new CallerContext(userId, role)), CancellationToken.None);
        Assert.Equal(expected, records.Count());
    }

    [Then(@"user (\d+) with role ""(.*)"" does not own the herd of rancher (\d+)")]
    public async Task ThenUserDoesNotOwnTheHerd(int userId, string role, int ownerId)
    {
        var owned = await world.CreateAnimalQueryService().Handle(
            new IsHerdOwnedByCallerQuery(world.HerdByOwner[ownerId].Id, new CallerContext(userId, role)),
            CancellationToken.None);
        Assert.False(owned);
    }

    [Then(@"user (\d+) with role ""(.*)"" owns its own herd")]
    public async Task ThenUserOwnsItsOwnHerd(int userId, string role)
    {
        var owned = await world.CreateAnimalQueryService().Handle(
            new IsHerdOwnedByCallerQuery(world.HerdByOwner[userId].Id, new CallerContext(userId, role)),
            CancellationToken.None);
        Assert.True(owned);
    }
}
