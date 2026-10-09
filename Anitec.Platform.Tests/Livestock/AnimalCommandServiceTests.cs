using Anitec.Platform.Livestock.Application.Internal.CommandServices;
using Anitec.Platform.Livestock.Domain.Model;
using Anitec.Platform.Livestock.Domain.Model.Commands;
using Anitec.Platform.Tests.Support;

namespace Anitec.Platform.Tests.Livestock;

public class AnimalCommandServiceTests
{
    private readonly InMemoryAnimalRepository _animals = new();
    private readonly InMemoryCorralRepository _corrals = new();
    private readonly CountingUnitOfWork _unitOfWork = new();

    private AnimalCommandService CreateService()
    {
        return new AnimalCommandService(_animals, _corrals, _unitOfWork);
    }

    private static CreateAnimalBatchCommand BatchOf(int quantity, int corralId = 1)
    {
        return new CreateAnimalBatchCommand("Gallina", "Criolla", "Hembra", null, 2m, "Saludable", 1, corralId,
            quantity, "Nacido en finca", "Adulto", null);
    }

    [Fact]
    public async Task CreateAnimal_PersistsTheAnimal()
    {
        var command = new CreateAnimalCommand("BOV-001", "Luna", "Bovino", "Brown Swiss", "Hembra", null, 410m,
            "Saludable", 1, null, null, null, null);

        var result = await CreateService().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("BOV-001", result.Value!.Tag);
        Assert.Single(_animals.Items);
        Assert.Equal(1, _unitOfWork.CompleteCalls);
    }

    [Fact]
    public async Task UpdateAnimal_WhenAnimalDoesNotExist_FailsWithAnimalNotFound()
    {
        var command = new UpdateAnimalCommand(99, "X", "X", "Bovino", "X", "Macho", null, 1m, "Saludable", 1, null,
            null, null, null);

        var result = await CreateService().Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LivestockError.AnimalNotFound, result.Error);
        Assert.Equal(0, _unitOfWork.CompleteCalls);
    }

    [Fact]
    public async Task DeleteAnimal_RemovesAnExistingAnimal()
    {
        var service = CreateService();
        await service.Handle(new CreateAnimalCommand("BOV-001", "Luna", "Bovino", "Brown Swiss", "Hembra", null, 410m,
            "Saludable", 1, null, null, null, null), CancellationToken.None);

        var result = await service.Handle(new DeleteAnimalCommand(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_animals.Items);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(501)]
    public async Task CreateBatch_WithQuantityOutsideOneToFiveHundred_Fails(int quantity)
    {
        _corrals.Seed("Corral 1", 1);

        var result = await CreateService().Handle(BatchOf(quantity), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_animals.Items);
        Assert.Equal(0, _unitOfWork.CompleteCalls);
    }

    [Fact]
    public async Task CreateBatch_WhenCorralDoesNotExist_FailsWithCorralNotFound()
    {
        var result = await CreateService().Handle(BatchOf(3, 42), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LivestockError.CorralNotFound, result.Error);
    }

    [Fact]
    public async Task CreateBatch_GeneratesSequentialTagsDerivedFromTheCorralName()
    {
        _corrals.Seed("Corral 1", 1);

        var result = await CreateService().Handle(BatchOf(3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Corral1-001", "Corral1-002", "Corral1-003" }, result.Value!.Select(a => a.Tag));
    }

    [Fact]
    public async Task CreateBatch_ContinuesTheSequenceWhenTheCorralAlreadyHasAnimals()
    {
        _corrals.Seed("Corral 1", 1);
        var service = CreateService();
        await service.Handle(BatchOf(2), CancellationToken.None);

        var second = await service.Handle(BatchOf(2), CancellationToken.None);

        Assert.Equal(new[] { "Corral1-003", "Corral1-004" }, second.Value!.Select(a => a.Tag));
    }

    [Fact]
    public async Task CreateBatch_PersistsTheWholeBatchInASingleTransaction()
    {
        _corrals.Seed("Corral 1", 1);

        var result = await CreateService().Handle(BatchOf(500), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(500, _animals.Items.Count);
        Assert.Equal(1, _unitOfWork.CompleteCalls);
    }

    [Fact]
    public async Task UpdateAnimalsStatus_UpdatesExistingAnimals_AndSkipsMissingIds()
    {
        _corrals.Seed("Corral 1", 1);
        var service = CreateService();
        await service.Handle(BatchOf(2), CancellationToken.None);

        var result = await service.Handle(new UpdateAnimalsStatusCommand(new List<int> { 1, 2, 999 }, "Vendido"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(_animals.Items, a => Assert.Equal("Vendido", a.Status));
    }

    [Fact]
    public async Task UpdateAnimalsStatus_WithNoSelection_Fails()
    {
        var result = await CreateService().Handle(new UpdateAnimalsStatusCommand(new List<int>(), "Vendido"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
