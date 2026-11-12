using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;

/// <summary>
///     Used only by the EF Core tools (<c>dotnet ef migrations add</c>). Creating a migration does not connect to
///     the database, so the default value holds no credentials; set <c>ANITEC_SUBSCRIPTIONS_CONNECTION_STRING</c>
///     to apply migrations from the command line.
/// </summary>
public class SubscriptionsDbContextFactory : IDesignTimeDbContextFactory<SubscriptionsDbContext>
{
    public SubscriptionsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ANITEC_SUBSCRIPTIONS_CONNECTION_STRING")
                               ?? "server=127.0.0.1;port=3306;user=root;database=anitec-subscriptions";

        var optionsBuilder = new DbContextOptionsBuilder<SubscriptionsDbContext>();
        optionsBuilder.UseMySQL(connectionString);

        return new SubscriptionsDbContext(optionsBuilder.Options);
    }
}
