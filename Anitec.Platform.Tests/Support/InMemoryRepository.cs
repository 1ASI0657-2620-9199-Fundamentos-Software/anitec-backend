using Anitec.Platform.Activities.Domain.Model.Entities;
using Anitec.Platform.Activities.Domain.Repositories;
using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Repositories;
using Anitec.Platform.Financial.Domain.Model.Entities;
using Anitec.Platform.Financial.Domain.Repositories;
using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Repositories;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Livestock.Domain.Repositories;
using Anitec.Platform.Sanitary.Domain.Model.Entities;
using Anitec.Platform.Sanitary.Domain.Repositories;
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

public class InMemoryHerdRepository()
    : InMemoryRepository<Herd>(h => h.Id, (h, id) => h.Id = id), IHerdRepository
{
    public Herd Seed(string name, int ownerId)
    {
        var herd = new Herd { Name = name, OwnerId = ownerId };
        AddAsync(herd).GetAwaiter().GetResult();
        return herd;
    }
}

public class InMemoryHealthEventRepository()
    : InMemoryRepository<HealthEvent>(e => e.Id, (e, id) => e.Id = id), IHealthEventRepository;

public class InMemoryFinancialRecordRepository()
    : InMemoryRepository<FinancialRecord>(r => r.Id, (r, id) => r.Id = id), IFinancialRecordRepository;

public class InMemoryFarmActivityRepository()
    : InMemoryRepository<FarmActivity>(a => a.Id, (a, id) => a.Id = id), IFarmActivityRepository;

public class InMemoryVeterinarianClientRepository()
    : InMemoryRepository<VeterinarianClient>(c => c.Id, (c, id) => c.Id = id), IVeterinarianClientRepository
{
    public Task<IEnumerable<VeterinarianClient>> FindByVeterinarianIdAsync(int veterinarianId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<VeterinarianClient>>(
            Items.Where(c => c.VeterinarianId == veterinarianId).ToList());
    }

    public Task<IEnumerable<VeterinarianClient>> FindByRancherIdAsync(int rancherId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<VeterinarianClient>>(
            Items.Where(c => c.RancherId == rancherId).ToList());
    }

    public Task<VeterinarianClient?> FindByVeterinarianIdAndRancherIdAsync(int veterinarianId, int rancherId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.FirstOrDefault(c => c.VeterinarianId == veterinarianId && c.RancherId == rancherId));
    }

    public Task<bool> ExistsByVeterinarianIdAndRancherIdAsync(int veterinarianId, int rancherId, CancellationToken cancellationToken)
    {
        return Task.FromResult(Items.Any(c => c.VeterinarianId == veterinarianId && c.RancherId == rancherId));
    }

    public VeterinarianClient Seed(int veterinarianId, int rancherId, string status)
    {
        var relationship = new VeterinarianClient
        {
            VeterinarianId = veterinarianId,
            RancherId = rancherId,
            Status = status
        };
        AddAsync(relationship).GetAwaiter().GetResult();
        return relationship;
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
