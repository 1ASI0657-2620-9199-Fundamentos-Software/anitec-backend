using System.Text.RegularExpressions;
using Anitec.Platform.Livestock.Application.Internal.CommandServices;
using Anitec.Platform.Livestock.Domain.Model.Commands;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class BulkRegistrationWorld
{
    public InMemoryAnimalRepository Animals { get; } = new();
    public InMemoryCorralRepository Corrals { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public Corral? Corral { get; set; }
    public Result<List<Animal>>? BatchResult { get; set; }
    public Result<List<Animal>>? StatusResult { get; set; }

    public AnimalCommandService CreateService()
    {
        return new AnimalCommandService(Animals, Corrals, UnitOfWork);
    }
}

[Binding]
public class BulkAnimalRegistrationSteps(BulkRegistrationWorld world)
{
    private static CreateAnimalBatchCommand BatchOf(int quantity, int corralId)
    {
        return new CreateAnimalBatchCommand("Gallina", "Criolla", "Hembra", null, 2m, "Saludable", 1, corralId,
            quantity, "Nacido en finca", "Adulto", null);
    }

    [Given(@"a corral named ""(.*)"" exists in herd (\d+)")]
    public void GivenACorralExists(string name, int herdId)
    {
        world.Corral = world.Corrals.Seed(name, herdId);
    }

    [Given(@"(\d+) animals were already registered in that corral")]
    public async Task GivenAnimalsWereAlreadyRegistered(int quantity)
    {
        var result = await world.CreateService()
            .Handle(BatchOf(quantity, world.Corral!.Id), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [When(@"I register (-?\d+) animals in that corral")]
    public async Task WhenIRegisterAnimalsInThatCorral(int quantity)
    {
        world.BatchResult = await world.CreateService()
            .Handle(BatchOf(quantity, world.Corral!.Id), CancellationToken.None);
    }

    [When(@"I register (-?\d+) animals in the corral with id (\d+)")]
    public async Task WhenIRegisterAnimalsInTheCorralWithId(int quantity, int corralId)
    {
        world.BatchResult = await world.CreateService()
            .Handle(BatchOf(quantity, corralId), CancellationToken.None);
    }

    [When(@"I mark the animals (.*) as ""(.*)""")]
    public async Task WhenIMarkTheAnimalsAs(string ids, string status)
    {
        var animalIds = Regex.Matches(ids, @"\d+").Select(m => int.Parse(m.Value)).ToList();
        world.StatusResult = await world.CreateService()
            .Handle(new UpdateAnimalsStatusCommand(animalIds, status), CancellationToken.None);
    }

    [Then(@"(\d+) animals are stored")]
    public void ThenAnimalsAreStored(int quantity)
    {
        Assert.Equal(quantity, world.Animals.Items.Count);
    }

    [Then("no animals are stored")]
    public void ThenNoAnimalsAreStored()
    {
        Assert.Empty(world.Animals.Items);
    }

    [Then(@"their tags are ""(.*)""")]
    public void ThenTheirTagsAre(string tags)
    {
        Assert.NotNull(world.BatchResult);
        var expected = tags.Split(", ");
        Assert.Equal(expected, world.BatchResult.Value!.Select(a => a.Tag));
    }

    [Then(@"the changes were saved in (\d+) transaction")]
    public void ThenTheChangesWereSavedInTransactions(int transactions)
    {
        Assert.Equal(transactions, world.UnitOfWork.CompleteCalls);
    }

    [Then("the registration fails")]
    public void ThenTheRegistrationFails()
    {
        Assert.NotNull(world.BatchResult);
        Assert.True(world.BatchResult.IsFailure);
    }

    [Then(@"the registration fails with the error ""(.*)""")]
    public void ThenTheRegistrationFailsWithTheError(string error)
    {
        Assert.NotNull(world.BatchResult);
        Assert.True(world.BatchResult.IsFailure);
        Assert.Equal(error, world.BatchResult.Error?.ToString());
    }

    [Then(@"all (\d+) animals have the status ""(.*)""")]
    public void ThenAllAnimalsHaveTheStatus(int quantity, string status)
    {
        Assert.NotNull(world.StatusResult);
        Assert.True(world.StatusResult.IsSuccess);
        Assert.Equal(quantity, world.Animals.Items.Count(a => a.Status == status));
    }
}
