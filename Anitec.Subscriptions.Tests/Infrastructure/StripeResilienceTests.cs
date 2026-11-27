using System.Net;
using Anitec.Subscriptions.Application.Internal.OutboundServices;
using Anitec.Subscriptions.Infrastructure.Payments.Stripe;
using Microsoft.Extensions.Options;
using Stripe;

namespace Anitec.Subscriptions.Tests.Infrastructure;

/// <summary>
///     Timeout and circuit breaker of the calls to Stripe (iteration 7). A fake HTTP server plays Stripe, so no real
///     call is made and no key is needed.
/// </summary>
public class StripeResilienceTests
{
    private const string FakeKey = "sk_test_not_a_real_key";

    private static readonly CheckoutSessionRequest Request = new(1, 2, "Ganadero Pro", 250, 99m, "PEN");

    /// <summary>Answers like Stripe and counts the calls that reach it.</summary>
    private sealed class FakeStripe(Func<int, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return answer(Interlocked.Increment(ref _calls), cancellationToken);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage StripeError(HttpStatusCode status)
    {
        return Json(status, "{\"error\":{\"type\":\"api_error\",\"message\":\"fake\"}}");
    }

    private static HttpResponseMessage Session()
    {
        return Json(HttpStatusCode.OK, "{\"id\":\"cs_test_1\",\"object\":\"checkout.session\",\"url\":\"https://checkout.example/cs_test_1\"}");
    }

    private static StripePaymentGateway NewGateway(FakeStripe stripe, StripeResilienceOptions? limits = null)
    {
        return new StripePaymentGateway(
            Options.Create(new StripeSettings
            {
                SecretKey = FakeKey,
                SuccessUrl = "https://app.example/success?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = "https://app.example/cancel"
            }),
            Options.Create(limits ?? new StripeResilienceOptions { BreakDurationSeconds = 1, TimeoutSeconds = 5 }),
            null,
            key => new StripeClient(key,
                httpClient: new SystemNetHttpClient(new HttpClient(stripe), maxNetworkRetries: 0)));
    }

    [Fact]
    public async Task ASuccessfulCall_ReturnsTheCheckoutSession()
    {
        var stripe = new FakeStripe((_, _) => Task.FromResult(Session()));

        var session = await NewGateway(stripe).CreateCheckoutSessionAsync(Request, CancellationToken.None);

        Assert.Equal("cs_test_1", session.Id);
        Assert.Equal(1, stripe.Calls);
    }

    [Fact]
    public async Task AfterRepeatedFailures_TheCircuitOpens_AndTheNextCallFailsAtOnceWithoutReachingStripe()
    {
        var stripe = new FakeStripe((_, _) => Task.FromResult(StripeError(HttpStatusCode.InternalServerError)));
        var gateway = NewGateway(stripe);

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<PaymentGatewayException>(() =>
                gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));
        var callsBeforeTheOpenCircuit = stripe.Calls;

        var started = DateTime.UtcNow;
        var unavailable = await Assert.ThrowsAsync<PaymentGatewayUnavailableException>(() =>
            gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMilliseconds(500), "an open circuit must answer at once");
        Assert.Equal(callsBeforeTheOpenCircuit, stripe.Calls);
        Assert.Equal(TimeSpan.FromSeconds(1), unavailable.RetryAfter);
    }

    [Fact]
    public async Task WhenStripeIsBack_TheCircuitClosesAgainAfterTheBreakTime()
    {
        var healthy = false;
        var stripe = new FakeStripe((_, _) =>
            Task.FromResult(healthy ? Session() : StripeError(HttpStatusCode.ServiceUnavailable)));
        var gateway = NewGateway(stripe);
        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<PaymentGatewayException>(() =>
                gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));
        await Assert.ThrowsAsync<PaymentGatewayUnavailableException>(() =>
            gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));

        healthy = true;
        await Task.Delay(TimeSpan.FromSeconds(1.3));
        var session = await gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None);

        Assert.Equal("cs_test_1", session.Id);
    }

    [Fact]
    public async Task TheErrorsOfTheUser_AreNotAnOutage_AndNeverOpenTheCircuit()
    {
        // 402 is a refused card and 400 a bad request: Stripe is working.
        var stripe = new FakeStripe((call, _) =>
            Task.FromResult(StripeError(call % 2 == 0 ? HttpStatusCode.PaymentRequired : HttpStatusCode.BadRequest)));
        var gateway = NewGateway(stripe);

        for (var i = 0; i < 8; i++)
            await Assert.ThrowsAsync<PaymentGatewayException>(() =>
                gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));

        Assert.Equal(8, stripe.Calls);
    }

    [Fact]
    public async Task ASessionThatDoesNotExist_ReturnsNull_AndDoesNotCountAsAFailure()
    {
        var stripe = new FakeStripe((_, _) => Task.FromResult(StripeError(HttpStatusCode.NotFound)));
        var gateway = NewGateway(stripe);

        for (var i = 0; i < 6; i++)
            Assert.Null(await gateway.GetCheckoutSessionAsync("cs_test_missing", CancellationToken.None));

        Assert.Equal(6, stripe.Calls);
    }

    [Fact]
    public async Task ACallThatTakesTooLong_IsCut_AndCountsAsAFailure()
    {
        var stripe = new FakeStripe(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return Session();
        });
        var gateway = NewGateway(stripe, new StripeResilienceOptions { TimeoutSeconds = 1, BreakDurationSeconds = 1 });

        var started = DateTime.UtcNow;
        await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5), "the call must be cut by the timeout");
    }

    [Fact]
    public async Task WithoutKeys_TheServiceStillSaysThatStripeIsNotConfigured()
    {
        var gateway = new StripePaymentGateway(Options.Create(new StripeSettings()));

        await Assert.ThrowsAsync<PaymentGatewayNotConfiguredException>(() =>
            gateway.CreateCheckoutSessionAsync(Request, CancellationToken.None));
    }
}
