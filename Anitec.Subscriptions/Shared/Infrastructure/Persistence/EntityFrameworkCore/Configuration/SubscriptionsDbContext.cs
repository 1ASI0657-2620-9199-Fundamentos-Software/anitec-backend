using Anitec.Subscriptions.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;

/// <summary>
///     Database context of the Subscriptions service. It owns the tables of plans, subscriptions and payments,
///     in a database of its own: no other service reads or writes them.
/// </summary>
/// <param name="options">The options for the database context</param>
public class SubscriptionsDbContext(DbContextOptions<SubscriptionsDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplySubscriptionsConfiguration();

        // Same naming convention as the core, so the tables keep their names when the data is copied.
        builder.UseSnakeCaseNamingConvention();
    }
}
