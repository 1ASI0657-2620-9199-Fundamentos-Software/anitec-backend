using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Tests.Support;

namespace Anitec.Platform.Tests.Shared;

public class RepositoryAndUnitOfWorkTests
{
    private static Animal NewAnimal(string tag)
    {
        return new Animal
        {
            Tag = tag, Name = tag, Species = "Bovino", Breed = "Holstein", Gender = "Hembra",
            Weight = 400, Status = "Saludable", HerdId = 1
        };
    }

    [Fact]
    public async Task AddAsync_IsNotPersisted_UntilUnitOfWorkCompletes()
    {
        var options = TestDb.NewOptions();
        await using var writer = new AppDbContext(options);
        var repository = new AnimalRepository(writer);
        var unitOfWork = new UnitOfWork(writer);

        await repository.AddAsync(NewAnimal("BOV-001"));
        await using (var beforeCommit = new AppDbContext(options))
        {
            Assert.Empty(await new AnimalRepository(beforeCommit).ListAsync());
        }

        await unitOfWork.CompleteAsync();

        await using var afterCommit = new AppDbContext(options);
        var animals = (await new AnimalRepository(afterCommit).ListAsync()).ToList();
        Assert.Single(animals);
        Assert.Equal("BOV-001", animals[0].Tag);
    }

    [Fact]
    public async Task UnitOfWork_PersistsABatchOfEntitiesTogether()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var repository = new AnimalRepository(context);

        for (var i = 1; i <= 50; i++) await repository.AddAsync(NewAnimal($"BOV-{i:000}"));
        await new UnitOfWork(context).CompleteAsync();

        await using var verify = new AppDbContext(options);
        Assert.Equal(50, (await new AnimalRepository(verify).ListAsync()).Count());
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsEntity_OrNullWhenMissing()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var repository = new AnimalRepository(context);
        await repository.AddAsync(NewAnimal("BOV-001"));
        await new UnitOfWork(context).CompleteAsync();

        var existing = await repository.FindByIdAsync(1);
        var missing = await repository.FindByIdAsync(999);

        Assert.NotNull(existing);
        Assert.Equal("BOV-001", existing.Tag);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Update_PersistsChangesMadeToAnEntity()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var repository = new AnimalRepository(context);
        var unitOfWork = new UnitOfWork(context);
        await repository.AddAsync(NewAnimal("BOV-001"));
        await unitOfWork.CompleteAsync();

        var animal = (await repository.FindByIdAsync(1))!;
        animal.Status = "En tratamiento";
        repository.Update(animal);
        await unitOfWork.CompleteAsync();

        await using var verify = new AppDbContext(options);
        Assert.Equal("En tratamiento", (await new AnimalRepository(verify).FindByIdAsync(1))!.Status);
    }

    [Fact]
    public async Task Remove_DeletesTheEntity()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var repository = new AnimalRepository(context);
        var unitOfWork = new UnitOfWork(context);
        await repository.AddAsync(NewAnimal("BOV-001"));
        await unitOfWork.CompleteAsync();

        repository.Remove((await repository.FindByIdAsync(1))!);
        await unitOfWork.CompleteAsync();

        await using var verify = new AppDbContext(options);
        Assert.Empty(await new AnimalRepository(verify).ListAsync());
    }
}
