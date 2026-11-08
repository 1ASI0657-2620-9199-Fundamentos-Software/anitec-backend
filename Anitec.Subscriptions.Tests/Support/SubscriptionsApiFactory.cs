using System.Net.Http.Headers;
using System.Text;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Domain.Model.Entities;
using Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Anitec.Subscriptions.Tests.Support;

/// <summary>
///     Starts the real Subscriptions API in memory. The database is an EF Core in-memory one, unless the variable
///     SUBSCRIPTIONS_TEST_MYSQL holds the connection string of a throwaway MySQL database: then the same tests run
///     against MySQL with the real migrations and unique indexes. The payment gateway is always a fake.
/// </summary>
public class SubscriptionsApiFactory : WebApplicationFactory<Program>
{
    public const string TokenSecret = "tests-only-secret-0123456789-abcdefghijklmnopqrstuvwxyz";
    private static int _nextUserId = Random.Shared.Next(10_000, 900_000);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly string _environment;

    public SubscriptionsApiFactory() : this("Production")
    {
    }

    protected SubscriptionsApiFactory(string environment)
    {
        _environment = environment;
        // Program reads these before the host is built, so they are set as environment variables.
        Environment.SetEnvironmentVariable("TokenSettings__Secret", TokenSecret);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", MySqlConnection ?? "server=unused");
    }

    public static string? MySqlConnection => Environment.GetEnvironmentVariable("SUBSCRIPTIONS_TEST_MYSQL");

    public static bool UsesMySql => !string.IsNullOrWhiteSpace(MySqlConnection);

    public FakePaymentGateway Gateway { get; } = new();

    /// <summary>A user id that no other test uses, so tests can share the database of their class.</summary>
    public static int NewUserId()
    {
        return Interlocked.Increment(ref _nextUserId);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureServices(services =>
        {
            if (!UsesMySql)
            {
                services.RemoveAll<DbContextOptions<SubscriptionsDbContext>>();
                // AddDbContext also registers the callback that configures MySQL (an internal EF Core type), so it
                // is found by name and removed too.
                foreach (var descriptor in services.Where(item =>
                                 item.ServiceType.IsGenericType
                                 && item.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration")
                                 && item.ServiceType.GenericTypeArguments.Contains(typeof(SubscriptionsDbContext)))
                             .ToList())
                    services.Remove(descriptor);
                services.AddDbContext<SubscriptionsDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            }

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Gateway);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (!UsesMySql)
        {
            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<SubscriptionsDbContext>().Database.EnsureCreated();
        }

        return host;
    }

    public HttpClient CreateClientFor(int userId, string role = "Rancher")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFactory.Create(userId, role));
        return client;
    }

    public async Task<HttpResponseMessage> PostWebhookAsync(string payload, string? signature)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/subscriptions/stripe/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (signature is not null) request.Headers.Add("Stripe-Signature", signature);
        return await client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> PayAndNotifyAsync(string sessionId)
    {
        Gateway.MarkPaid(sessionId);
        var (payload, signature) = Gateway.BuildWebhook(sessionId);
        return await PostWebhookAsync(payload, signature);
    }

    public async Task<T> WithDbAsync<T>(Func<SubscriptionsDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<SubscriptionsDbContext>());
    }

    public Task<List<Payment>> PaymentsOfAsync(int userId)
    {
        return WithDbAsync(db => db.Set<Payment>().Where(p => p.UserId == userId).ToListAsync());
    }

    public Task<List<Subscription>> SubscriptionsOfAsync(int userId)
    {
        return WithDbAsync(db => db.Set<Subscription>().Where(s => s.UserId == userId).ToListAsync());
    }
}
