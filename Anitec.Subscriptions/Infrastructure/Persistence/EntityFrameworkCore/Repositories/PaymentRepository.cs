using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Subscriptions.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class PaymentRepository(SubscriptionsDbContext context) : BaseRepository<Payment>(context), IPaymentRepository
{
    public async Task<IEnumerable<Payment>> FindByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        return await Context.Set<Payment>()
            .Where(payment => payment.UserId == userId)
            .OrderByDescending(payment => payment.PaidAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payment?> FindByProviderPaymentIdAsync(string providerPaymentId, CancellationToken cancellationToken)
    {
        return await Context.Set<Payment>()
            .FirstOrDefaultAsync(payment => payment.ProviderPaymentId == providerPaymentId, cancellationToken);
    }
}
