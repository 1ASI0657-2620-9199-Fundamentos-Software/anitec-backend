using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Repositories;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Repositories;
using Anitec.Platform.Shared.Domain.Repositories;

namespace Anitec.Platform.Tests.Support;

public abstract class InMemoryRepository<TEntity>(Func<TEntity, int> idSelector, Action<TEntity, int>? idSetter = null)
    : IBaseRepository<TEntity> where TEntity : class
{
    private int _nextId = 1;
    public List<TEntity> Items { get; } = [];

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        idSetter?.Invoke(entity, _nextId++);
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task<TEntity?> FindByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Items.FirstOrDefault(e => idSelector(e) == id));
    }

    public void Update(TEntity entity)
    {
    }

    public void Remove(TEntity entity)
    {
        Items.Remove(entity);
    }

    public Task<IEnumerable<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<TEntity>>(Items.ToList());
    }
}

public class InMemoryAnimalRepository()
    : InMemoryRepository<Animal>(a => a.Id, (a, id) => a.Id = id), IAnimalRepository;

public class InMemoryCorralRepository()
    : InMemoryRepository<Corral>(c => c.Id, (c, id) => c.Id = id), ICorralRepository
{
    public Corral Seed(string name, int herdId)
    {
        var corral = new Corral { Name = name, HerdId = herdId };
        Items.Add(corral);
        corral.Id = Items.Count;
        return corral;
    }
}

public class InMemoryUserRepository() : InMemoryRepository<User>(u => u.Id), IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.FirstOrDefault(u => u.Username == username));
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Any(u => u.Username == username));
    }
}

public class CountingUnitOfWork : IUnitOfWork
{
    public int CompleteCalls { get; private set; }

    public Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        CompleteCalls++;
        return Task.CompletedTask;
    }
}
