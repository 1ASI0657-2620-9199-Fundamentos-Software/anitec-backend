using System.Net;
using Anitec.Platform.Livestock.Application.Internal.OutboundServices;
using Anitec.Platform.Livestock.Infrastructure.Subscriptions;
using Anitec.Platform.Shared.Application.AuditLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Anitec.Platform.Tests.Livestock;

/// <summary>
///     The first call from the core to another service. A fake HTTP server plays Subscriptions. The call has a timeout
///     and a circuit breaker and never throws for a failure of the service (QAS-09, iteration 7).
/// </summary>
public class SubscriptionPlanClientTests
{
    private sealed class FakeSubscriptions(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            lock (Requests)
            {
                Requests.Add(request);
            }

            return answer(request, cancellationToken);
        }
    }

    private sealed class FixedCorrelation(string? id) : ICorrelationIdAccessor
    {
        public string? CorrelationId => id;
    }

    private static Task<HttpResponseMessage> Plan()
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"subscriptionId\":4,\"planId\":1,\"planName\":\"Ganadero Basico\",\"maxAnimals\":80,\"status\":\"Active\"}",
                System.Text.Encoding.UTF8, "application/json")
        });
    }

    private static Task<HttpResponseMessage> Status(HttpStatusCode code)
    {
        return Task.FromResult(new HttpResponseMessage(code));
    }

    private static ISubscriptionPlanClient NewClient(FakeSubscriptions server, SubscriptionsClientOptions? options = null,
        string? correlationId = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICorrelationIdAccessor>(new FixedCorrelation(correlationId));
        services.AddSingleton(Options.Create(options ?? new SubscriptionsClientOptions
        {
            BaseUrl = "http://subscriptions:8080",
            TimeoutSeconds = 2,
            BreakDurationSeconds = 1
        }));
        services.AddSubscriptionPlanClient();
        // The same named client: only the server behind it is replaced by the fake.
        services.AddHttpClient(SubscriptionsClientRegistration.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => server);
        return services.BuildServiceProvider().GetRequiredService<ISubscriptionPlanClient>();
    }

    [Fact]
    public async Task AnActivePlan_IsReturned_AndTheTokenAndTheCorrelationIdAreForwarded()
    {
        var server = new FakeSubscriptions((_, _) => Plan());

        var result = await NewClient(server, correlationId: "req-42").GetActivePlanAsync("jwt-of-the-rancher", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Found, result.Status);
        Assert.Equal(80, result.Plan!.MaxAnimals);
        Assert.Equal("Ganadero Basico", result.Plan.PlanName);
        var request = Assert.Single(server.Requests);
        Assert.Equal("http://subscriptions:8080/api/v1/subscriptions/active-plan", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("jwt-of-the-rancher", request.Headers.Authorization.Parameter);
        Assert.Equal("req-42", request.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task WhenThereIsNoActiveSubscription_TheAnswerIsNoActivePlan()
    {
        var server = new FakeSubscriptions((_, _) => Status(HttpStatusCode.NotFound));

        var result = await NewClient(server).GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.NoActivePlan, result.Status);
    }

    [Fact]
    public async Task WithoutAnAddress_NoCallIsMade()
    {
        var server = new FakeSubscriptions((_, _) => Plan());

        var result = await NewClient(server, new SubscriptionsClientOptions { BaseUrl = "" })
            .GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.NotConfigured, result.Status);
        Assert.Equal(0, server.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task AFailureOfTheService_IsUnavailable_NotAnException(HttpStatusCode code)
    {
        var server = new FakeSubscriptions((_, _) => Status(code));

        var result = await NewClient(server).GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task WhenTheServiceIsNotReachable_ItIsUnavailable()
    {
        var server = new FakeSubscriptions((_, _) => throw new HttpRequestException("connection refused"));

        var result = await NewClient(server).GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task ASlowService_IsCutByTheTimeout_SoTheRancherDoesNotWait()
    {
        var server = new FakeSubscriptions(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var options = new SubscriptionsClientOptions { BaseUrl = "http://subscriptions:8080", TimeoutSeconds = 1 };

        var started = DateTime.UtcNow;
        var result = await NewClient(server, options).GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Unavailable, result.Status);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5), "the call must be cut by the timeout");
    }

    [Fact]
    public async Task AfterRepeatedFailures_TheCircuitOpens_AndTheNextCallDoesNotReachTheService()
    {
        var server = new FakeSubscriptions((_, _) => Status(HttpStatusCode.InternalServerError));
        var client = NewClient(server);

        for (var i = 0; i < 3; i++) await client.GetActivePlanAsync("jwt", CancellationToken.None);
        var callsBeforeTheOpenCircuit = server.Calls;
        var started = DateTime.UtcNow;
        var result = await client.GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Unavailable, result.Status);
        Assert.Equal(callsBeforeTheOpenCircuit, server.Calls);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(500), "an open circuit must answer at once");
    }

    [Fact]
    public async Task WhenTheServiceIsBack_TheCircuitClosesAfterTheBreakTime()
    {
        var healthy = false;
        var server = new FakeSubscriptions((_, _) => healthy ? Plan() : Status(HttpStatusCode.ServiceUnavailable));
        var client = NewClient(server);
        for (var i = 0; i < 4; i++) await client.GetActivePlanAsync("jwt", CancellationToken.None);

        healthy = true;
        await Task.Delay(TimeSpan.FromSeconds(1.3));
        var result = await client.GetActivePlanAsync("jwt", CancellationToken.None);

        Assert.Equal(PlanLookupStatus.Found, result.Status);
    }

    [Fact]
    public async Task ARancherWithoutAPlan_IsNotAFailure_SoTheCircuitStaysClosed()
    {
        var server = new FakeSubscriptions((_, _) => Status(HttpStatusCode.NotFound));
        var client = NewClient(server);

        for (var i = 0; i < 8; i++)
            Assert.Equal(PlanLookupStatus.NoActivePlan, (await client.GetActivePlanAsync("jwt", CancellationToken.None)).Status);

        Assert.Equal(8, server.Calls);
    }
}
