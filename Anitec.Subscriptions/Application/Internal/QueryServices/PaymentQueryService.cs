using Anitec.Subscriptions.Application.QueryServices;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;

namespace Anitec.Subscriptions.Application.Internal.QueryServices;

public class PaymentQueryService(IPaymentRepository repository) : IPaymentQueryService
{
    public async Task<IEnumerable<Payment>> Handle(int userId, CancellationToken cancellationToken)
    {
        return await repository.FindByUserIdAsync(userId, cancellationToken);
    }

    public async Task<Payment?> Handle(string providerPaymentId, CancellationToken cancellationToken)
    {
        return await repository.FindByProviderPaymentIdAsync(providerPaymentId, cancellationToken);
    }
}
