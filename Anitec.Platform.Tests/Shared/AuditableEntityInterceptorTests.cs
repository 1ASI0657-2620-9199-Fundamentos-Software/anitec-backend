using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Platform.Tests.Support;

namespace Anitec.Platform.Tests.Shared;

public class AuditableEntityInterceptorTests
{
    [Fact]
    public async Task SavingAnAuditableEntity_SetsCreatedAtAndUpdatedAt()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var before = DateTimeOffset.UtcNow;

        await new UserRepository(context).AddAsync(new User("ganadero", "hash", "Carlos Mendoza", "Rancher"));
        await new UnitOfWork(context).CompleteAsync();

        var saved = (await new UserRepository(context).FindByUsernameAsync("ganadero", CancellationToken.None))!;
        Assert.NotNull(saved.CreatedAt);
        Assert.NotNull(saved.UpdatedAt);
        Assert.True(saved.CreatedAt >= before);
    }

    [Fact]
    public async Task UpdatingAnAuditableEntity_RefreshesUpdatedAt_AndKeepsCreatedAt()
    {
        var options = TestDb.NewOptions();
        await using var context = new AppDbContext(options);
        var repository = new UserRepository(context);
        var unitOfWork = new UnitOfWork(context);
        await repository.AddAsync(new User("ganadero", "hash", "Carlos Mendoza", "Rancher"));
        await unitOfWork.CompleteAsync();
        var user = (await repository.FindByUsernameAsync("ganadero", CancellationToken.None))!;
        var createdAt = user.CreatedAt;
        var firstUpdate = user.UpdatedAt;

        await Task.Delay(20);
        user.UpdateProfile("Carlos A. Mendoza", "Rancher");
        repository.Update(user);
        await unitOfWork.CompleteAsync();

        Assert.Equal(createdAt, user.CreatedAt);
        Assert.True(user.UpdatedAt > firstUpdate);
    }
}
