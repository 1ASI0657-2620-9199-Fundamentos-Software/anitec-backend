using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Domain.Repositories;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

namespace Anitec.Subscriptions.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class SubscriptionPlanRepository(SubscriptionsDbContext context) : BaseRepository<SubscriptionPlan>(context), ISubscriptionPlanRepository
{
}
