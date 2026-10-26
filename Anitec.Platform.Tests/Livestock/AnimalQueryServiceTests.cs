using Anitec.Platform.Activities.Application.Internal.QueryServices;
using Anitec.Platform.Activities.Domain.Model.Entities;
using Anitec.Platform.Activities.Domain.Model.Queries;
using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Clients.Domain.Model;
using Anitec.Platform.Livestock.Application.Internal.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Tests.Support;

namespace Anitec.Platform.Tests.Livestock;

public class AnimalQueryServiceTests
{
    private static readonly CallerContext Ana = new(1, "Rancher");
    private static readonly CallerContext Luis = new(2, "Rancher");
    private static readonly CallerContext Vet = new(10, "Veterinarian");

    private readonly InMemoryAnimalRepository _animals = new();
    private readonly InMemoryHerdRepository _herds = new();
    private readonly InMemoryVeterinarianClientRepository _relationships = new();

    private AnimalQueryService CreateService()
    {
        return new AnimalQueryService(_animals, _herds, new VeterinarianClientQueryService(_relationships));
    }

    private async Task<Animal> SeedAnimalAsync(int ownerId)
    {
        var herd = _herds.Items.FirstOrDefault(item => item.OwnerId == ownerId) ?? _herds.Seed($"Herd {ownerId}", ownerId);
        var animal = new Animal { Tag = $"A-{_animals.Items.Count + 1}", HerdId = herd.Id };
        await _animals.AddAsync(animal);
        return animal;
    }

    [Fact]
    public async Task AreAnimalsOwnedByCaller_WhenAllBelongToTheRancher_ReturnsTrue()
    {
        var first = await SeedAnimalAsync(Ana.Id);
        var second = await SeedAnimalAsync(Ana.Id);

        var result = await CreateService().Handle(
            new AreAnimalsOwnedByCallerQuery([first.Id, second.Id], Ana), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task AreAnimalsOwnedByCaller_WhenOneBelongsToAnotherRancher_ReturnsFalse()
    {
        var own = await SeedAnimalAsync(Ana.Id);
        var foreign = await SeedAnimalAsync(Luis.Id);

        var result = await CreateService().Handle(
            new AreAnimalsOwnedByCallerQuery([own.Id, foreign.Id], Ana), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAnimalsOwnedByCaller_WhenTheAnimalDoesNotExist_ReturnsFalse()
    {
        await SeedAnimalAsync(Ana.Id);

        var result = await CreateService().Handle(
            new AreAnimalsOwnedByCallerQuery([999], Ana), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAnimalsOwnedByCaller_WhenTheCallerIsAVeterinarian_ReturnsFalseEvenWithAnAcceptedRelationship()
    {
        var animal = await SeedAnimalAsync(Ana.Id);
        _relationships.Seed(Vet.Id, Ana.Id, VeterinarianClientStatus.Accepted);

        var result = await CreateService().Handle(
            new AreAnimalsOwnedByCallerQuery([animal.Id], Vet), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task AreAnimalsOwnedByCaller_WithoutIds_ReturnsFalse()
    {
        await SeedAnimalAsync(Ana.Id);

        var result = await CreateService().Handle(
            new AreAnimalsOwnedByCallerQuery([], Ana), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetAnimalById_WhenTheVeterinarianWasAccepted_ReturnsTheAnimal()
    {
        var animal = await SeedAnimalAsync(Ana.Id);
        _relationships.Seed(Vet.Id, Ana.Id, VeterinarianClientStatus.Accepted);

        var result = await CreateService().Handle(
            new GetAnimalByIdForCallerQuery(animal.Id, Vet), CancellationToken.None);

        Assert.Equal(animal.Id, result?.Id);
    }

    [Fact]
    public async Task GetAnimalById_WhenTheCallerHasAnUnknownRole_ReturnsNull()
    {
        var animal = await SeedAnimalAsync(Ana.Id);

        var result = await CreateService().Handle(
            new GetAnimalByIdForCallerQuery(animal.Id, new CallerContext(5, "Admin")), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FarmActivities_AVeterinarianSeesTheOnesAssignedToItAndThoseOfAcceptedClients()
    {
        _relationships.Seed(Vet.Id, Ana.Id, VeterinarianClientStatus.Accepted);
        _relationships.Seed(Vet.Id, Luis.Id, VeterinarianClientStatus.Pending);
        var activities = new InMemoryFarmActivityRepository();
        await activities.AddAsync(new FarmActivity { OwnerId = Ana.Id });
        await activities.AddAsync(new FarmActivity { OwnerId = Luis.Id });
        await activities.AddAsync(new FarmActivity { OwnerId = Luis.Id, VeterinarianId = Vet.Id });
        var service = new FarmActivityQueryService(activities, new VeterinarianClientQueryService(_relationships));

        var visible = (await service.Handle(new GetFarmActivitiesForCallerQuery(Vet), CancellationToken.None)).ToList();

        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, activity => activity.OwnerId == Luis.Id && activity.VeterinarianId is null);
    }

    [Fact]
    public async Task FarmActivities_ARancherDoesNotSeeTheActivitiesOfOtherRanchers()
    {
        var activities = new InMemoryFarmActivityRepository();
        await activities.AddAsync(new FarmActivity { OwnerId = Ana.Id });
        await activities.AddAsync(new FarmActivity { OwnerId = Luis.Id });
        var service = new FarmActivityQueryService(activities, new VeterinarianClientQueryService(_relationships));

        var visible = await service.Handle(new GetFarmActivitiesForCallerQuery(Ana), CancellationToken.None);

        Assert.Single(visible);
    }
}
