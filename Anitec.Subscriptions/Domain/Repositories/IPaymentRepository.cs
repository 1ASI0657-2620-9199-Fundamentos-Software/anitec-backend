using Anitec.Subscriptions.Shared.Domain.Repositories;
using Anitec.Subscriptions.Domain.Model.Entities;

namespace Anitec.Subscriptions.Domain.Repositories;

public interface IPaymentRepository : IBaseRepository<Payment>
{
    Task<IEnumerable<Payment>> FindByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<Payment?> FindByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken);
}
