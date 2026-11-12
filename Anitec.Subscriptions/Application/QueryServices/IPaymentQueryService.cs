using Anitec.Subscriptions.Domain.Model.Entities;

namespace Anitec.Subscriptions.Application.QueryServices;

public interface IPaymentQueryService
{
    Task<IEnumerable<Payment>> Handle(int userId, CancellationToken cancellationToken);
    Task<Payment?> Handle(string providerPaymentId, CancellationToken cancellationToken);
}
