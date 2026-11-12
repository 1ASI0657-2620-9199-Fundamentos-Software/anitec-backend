using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Anitec.Subscriptions.Tests.Support;
using Reqnroll;

namespace Anitec.Subscriptions.Tests.Features.StepDefinitions;

/// <summary>
///     State of one scenario: the real Subscriptions API in memory (fake payment gateway), the people of the
///     scenario and the last answer. Reqnroll creates one per scenario and disposes it at the end.
/// </summary>
public class SubscriptionsWorld : IDisposable
{
    public const string Visitor = "visitor";

    public static readonly IReadOnlyDictionary<string, int> PlanIds = new Dictionary<string, int>
    {
        ["Ganadero Basico"] = 1,
        ["Ganadero Pro"] = 2,
        ["Veterinario Profesional"] = 3
    };

    public SubscriptionsApiFactory Factory { get; } = new();
    public Dictionary<string, int> UserIds { get; } = new();
    public Dictionary<string, HttpClient> Clients { get; } = new();
    public Dictionary<string, string> SessionIds { get; } = new();
    public HttpResponseMessage? Response { get; set; }
    public List<HttpResponseMessage> Responses { get; } = [];
    public JsonElement Body { get; set; }
    public (string Payload, string Signature)? Notification { get; set; }

    public void Dispose()
    {
        foreach (var client in Clients.Values) client.Dispose();
        Factory.Dispose();
    }

    public int IdOf(string name)
    {
        return UserIds[name];
    }

    public HttpClient ClientOf(string name)
    {
        return Clients[name];
    }

    public async Task SetResponseAsync(HttpResponseMessage response)
    {
        Response = response;
        Responses.Add(response);
        var text = await response.Content.ReadAsStringAsync();
        Body = string.IsNullOrWhiteSpace(text) || !(response.Content.Headers.ContentType?.MediaType ?? "").Contains("json")
            ? default
            : JsonDocument.Parse(text).RootElement.Clone();
    }
}

[Binding]
public class SubscriptionsSteps(SubscriptionsWorld world)
{
    // ----- Given: who is calling

    [Given(@"""(.*)"" is signed in as a ""(.*)""")]
    public void GivenSignedInAs(string name, string role)
    {
        var id = SubscriptionsApiFactory.NewUserId();
        world.UserIds[name] = id;
        world.Clients[name] = world.Factory.CreateClientFor(id, role);
    }

    [Given("a visitor without a token")]
    public void GivenAVisitorWithoutAToken()
    {
        world.Clients[SubscriptionsWorld.Visitor] = world.Factory.CreateClient();
    }

    [Given("a visitor with a token signed with another secret")]
    public void GivenAVisitorWithAForeignToken()
    {
        world.Clients[SubscriptionsWorld.Visitor] = ClientWithToken(
            TokenFactory.Create(1, "Rancher", "another-secret-0123456789-abcdefghijklmnopqrstuvwxyz"));
    }

    [Given("a visitor with an expired token")]
    public void GivenAVisitorWithAnExpiredToken()
    {
        world.Clients[SubscriptionsWorld.Visitor] = ClientWithToken(
            TokenFactory.Create(1, "Rancher", lifetime: TimeSpan.FromMinutes(-5)));
    }

    // ----- Given: state of Stripe and of the payments

    [Given("Stripe has no keys configured")]
    public void GivenStripeHasNoKeys()
    {
        world.Factory.Gateway.NotConfigured = true;
    }

    [Given("Stripe is not reachable")]
    public void GivenStripeIsNotReachable()
    {
        world.Factory.Gateway.Failing = true;
    }

    [Given(@"""(.*)"" opened a checkout for the plan ""(.*)""")]
    public async Task GivenOpenedACheckout(string name, string plan)
    {
        await OpenCheckoutAsync(name, SubscriptionsWorld.PlanIds[plan]);
    }

    [Given(@"""(.*)"" paid that checkout")]
    public void GivenPaidThatCheckout(string name)
    {
        world.Factory.Gateway.MarkPaid(world.SessionIds[name]);
    }

    [Given(@"""(.*)"" has paid the plan ""(.*)""")]
    public async Task GivenHasPaidThePlan(string name, string plan)
    {
        var sessionId = await OpenCheckoutAsync(name, SubscriptionsWorld.PlanIds[plan]);
        var answer = await world.Factory.PayAndNotifyAsync(sessionId);
        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
    }

    [Given("Stripe has a paid session that this service did not open")]
    public void GivenAForeignPaidSession()
    {
        world.SessionIds["foreign"] = world.Factory.Gateway.AddForeignPaidSession();
    }

    // ----- When: plans, subscriptions and payments

    [When(@"""(.*)"" lists the subscription plans")]
    public async Task WhenListsThePlans(string name)
    {
        await world.SetResponseAsync(await world.ClientOf(name).GetAsync("/api/v1/subscription-plans"));
    }

    [When("the visitor lists the subscription plans")]
    public async Task WhenTheVisitorListsThePlans()
    {
        await WhenListsThePlans(SubscriptionsWorld.Visitor);
    }

    [When(@"""(.*)"" asks for the active subscription of ""(.*)""")]
    public async Task WhenAsksForTheActiveSubscription(string caller, string owner)
    {
        await world.SetResponseAsync(await world.ClientOf(caller)
            .GetAsync($"/api/v1/subscriptions/users/{world.IdOf(owner)}/active"));
    }

    [When(@"""(.*)"" asks for the payments of ""(.*)""")]
    public async Task WhenAsksForThePayments(string caller, string owner)
    {
        await world.SetResponseAsync(await world.ClientOf(caller)
            .GetAsync($"/api/v1/subscriptions/users/{world.IdOf(owner)}/payments"));
    }

    [When(@"""(.*)"" asks for the subscription of ""(.*)"" by id")]
    public async Task WhenAsksForTheSubscriptionById(string caller, string owner)
    {
        var subscriptionId = (await world.Factory.SubscriptionsOfAsync(world.IdOf(owner))).First().Id;
        await world.SetResponseAsync(await world.ClientOf(caller).GetAsync($"/api/v1/subscriptions/{subscriptionId}"));
    }

    [When(@"""(.*)"" lists the subscriptions")]
    public async Task WhenListsTheSubscriptions(string caller)
    {
        await world.SetResponseAsync(await world.ClientOf(caller).GetAsync("/api/v1/subscriptions"));
    }

    // ----- When: checkout and confirmation

    [When(@"""(.*)"" starts a checkout for the plan ""(.*)""")]
    public async Task WhenStartsACheckout(string name, string plan)
    {
        await StartCheckoutAsync(name, SubscriptionsWorld.PlanIds[plan]);
    }

    [When(@"""(.*)"" starts a checkout for the plan with id (\d+)")]
    public async Task WhenStartsACheckoutForId(string name, int planId)
    {
        await StartCheckoutAsync(name, planId);
    }

    [When(@"""(.*)"" confirms the checkout")]
    public async Task WhenConfirmsTheCheckout(string name)
    {
        await ConfirmAsync(name, world.SessionIds[name]);
    }

    [When(@"""(.*)"" confirms the checkout twice")]
    public async Task WhenConfirmsTheCheckoutTwice(string name)
    {
        await ConfirmAsync(name, world.SessionIds[name]);
        await ConfirmAsync(name, world.SessionIds[name]);
    }

    [When(@"""(.*)"" confirms the checkout of ""(.*)""")]
    public async Task WhenConfirmsTheCheckoutOf(string caller, string owner)
    {
        await ConfirmAsync(caller, world.SessionIds[owner]);
    }

    [When("\"(.*)\" confirms a checkout that does not exist")]
    public async Task WhenConfirmsAnUnknownCheckout(string name)
    {
        await ConfirmAsync(name, "cs_test_unknown");
    }

    [When(@"""(.*)"" confirms that session")]
    public async Task WhenConfirmsTheForeignSession(string name)
    {
        await ConfirmAsync(name, world.SessionIds["foreign"]);
    }

    // ----- When: notifications of Stripe (the webhook is anonymous: its security is the signature)

    [When(@"Stripe notifies the payment of the checkout of ""(.*)""")]
    public async Task WhenStripeNotifies(string name)
    {
        world.Notification = world.Factory.Gateway.BuildWebhook(world.SessionIds[name]);
        await PostNotificationAsync(world.Notification.Value.Payload, world.Notification.Value.Signature);
    }

    [When("Stripe notifies the same payment again")]
    public async Task WhenStripeNotifiesAgain()
    {
        var (payload, signature) = world.Notification!.Value;
        await PostNotificationAsync(payload, signature);
    }

    [When(@"Stripe sends a notification of type ""(.*)"" for the checkout of ""(.*)""")]
    public async Task WhenStripeSendsAnotherType(string type, string name)
    {
        var (payload, signature) = world.Factory.Gateway.BuildWebhook(world.SessionIds[name], type);
        await PostNotificationAsync(payload, signature);
    }

    [When(@"someone notifies the payment of the checkout of ""(.*)"" with a forged signature")]
    public async Task WhenSomeoneForgesTheSignature(string name)
    {
        var (payload, _) = world.Factory.Gateway.BuildWebhook(world.SessionIds[name]);
        await PostNotificationAsync(payload, "forged-signature");
    }

    [When(@"someone changes the body of the notification of the checkout of ""(.*)"" after signing it")]
    public async Task WhenSomeoneChangesTheBody(string name)
    {
        var (payload, signature) = world.Factory.Gateway.BuildWebhook(world.SessionIds[name]);
        await PostNotificationAsync(
            payload.Replace("checkout.session.completed", "checkout.session.expired"), signature);
    }

    [When(@"someone notifies the payment of the checkout of ""(.*)"" without a signature")]
    public async Task WhenSomeoneOmitsTheSignature(string name)
    {
        var (payload, _) = world.Factory.Gateway.BuildWebhook(world.SessionIds[name]);
        await PostNotificationAsync(payload, null);
    }

    [When("someone notifies an empty event with any signature")]
    public async Task WhenSomeoneNotifiesAnEmptyEvent()
    {
        await PostNotificationAsync("{}", "anything");
    }

    // ----- Then: answers

    [Then(@"the response status is (\d+)")]
    public void ThenTheResponseStatusIs(int status)
    {
        Assert.NotNull(world.Response);
        Assert.Equal(status, (int)world.Response.StatusCode);
    }

    [Then(@"both answers have status (\d+)")]
    public void ThenBothAnswersHaveStatus(int status)
    {
        Assert.Equal(2, world.Responses.Count);
        Assert.All(world.Responses, response => Assert.Equal(status, (int)response.StatusCode));
    }

    [Then(@"the plan ""(.*)"" costs (\d+)")]
    public void ThenThePlanCosts(string name, decimal price)
    {
        // A database shared by several scenarios (MySQL mode) may hold other plans: look for the one by name.
        var plan = world.Body.EnumerateArray().First(item => item.GetProperty("name").GetString() == name);
        Assert.Equal(price, plan.GetProperty("price").GetDecimal());
    }

    [Then(@"the subscription is for the plan ""(.*)""")]
    public void ThenTheSubscriptionIsForThePlan(string plan)
    {
        Assert.Equal(SubscriptionsWorld.PlanIds[plan], world.Body.GetProperty("planId").GetInt32());
    }

    [Then(@"the subscription status is ""(.*)""")]
    public void ThenTheSubscriptionStatusIs(string status)
    {
        Assert.Equal(status, world.Body.GetProperty("status").GetString());
    }

    [Then(@"the list has (\d+) items")]
    public void ThenTheListHasItems(int count)
    {
        Assert.Equal(count, world.Body.GetArrayLength());
    }

    [Then(@"the first payment is (\d+) PEN with status ""(.*)""")]
    public void ThenTheFirstPaymentIs(decimal amount, string status)
    {
        var payment = world.Body.EnumerateArray().First();
        Assert.Equal(amount, payment.GetProperty("amount").GetDecimal());
        Assert.Equal("PEN", payment.GetProperty("currency").GetString());
        Assert.Equal(status, payment.GetProperty("status").GetString());
    }

    [Then(@"""(.*)"" can open the subscription of ""(.*)"" by id")]
    public async Task ThenTheOwnerCanOpenIt(string caller, string owner)
    {
        var subscriptionId = (await world.Factory.SubscriptionsOfAsync(world.IdOf(owner))).First().Id;
        var response = await world.ClientOf(caller).GetAsync($"/api/v1/subscriptions/{subscriptionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Then("the answer has the address of the payment page")]
    public void ThenTheAnswerHasThePaymentPageAddress()
    {
        Assert.StartsWith("cs_test_", world.Body.GetProperty("sessionId").GetString());
        Assert.StartsWith("https://checkout.test/", world.Body.GetProperty("checkoutUrl").GetString());
    }

    [Then(@"Stripe was asked to charge (\d+) PEN to ""(.*)"" for the plan ""(.*)""")]
    public void ThenStripeWasAskedToCharge(decimal price, string name, string plan)
    {
        var request = world.Factory.Gateway.Requests.Last(item => item.UserId == world.IdOf(name));
        Assert.Equal(price, request.Price);
        Assert.Equal("PEN", request.Currency);
        Assert.Equal(SubscriptionsWorld.PlanIds[plan], request.PlanId);
    }

    [Then("the answer does not reveal the Stripe configuration")]
    public async Task ThenTheAnswerDoesNotRevealTheConfiguration()
    {
        Assert.DoesNotContain("StripeSettings", await world.Response!.Content.ReadAsStringAsync());
    }

    [Then("the notification is reported as handled")]
    public void ThenReportedAsHandled()
    {
        Assert.True(world.Body.GetProperty("handled").GetBoolean());
        Assert.False(world.Body.GetProperty("duplicate").GetBoolean());
    }

    [Then("the notification is reported as already handled")]
    public void ThenReportedAsDuplicate()
    {
        Assert.True(world.Body.GetProperty("duplicate").GetBoolean());
    }

    [Then("the notification is reported as ignored")]
    public void ThenReportedAsIgnored()
    {
        Assert.False(world.Body.GetProperty("handled").GetBoolean());
    }

    // ----- Then: what was stored

    [Then(@"""(.*)"" has (\d+) payments and (\d+) subscriptions")]
    public async Task ThenHasStored(string name, int payments, int subscriptions)
    {
        Assert.Equal(payments, (await world.Factory.PaymentsOfAsync(world.IdOf(name))).Count);
        Assert.Equal(subscriptions, (await world.Factory.SubscriptionsOfAsync(world.IdOf(name))).Count);
    }

    [Then(@"the payment of ""(.*)"" is (\d+) PEN paid through ""(.*)""")]
    public async Task ThenThePaymentIs(string name, decimal amount, string provider)
    {
        var payment = Assert.Single(await world.Factory.PaymentsOfAsync(world.IdOf(name)));
        Assert.Equal(amount, payment.Amount);
        Assert.Equal("PEN", payment.Currency);
        Assert.Equal(provider, payment.Provider);
        Assert.Equal("Paid", payment.Status);
        Assert.Equal(world.SessionIds[name], payment.ProviderPaymentId);
    }

    [Then(@"the subscription status of ""(.*)"" is ""(.*)""")]
    public async Task ThenTheStoredSubscriptionStatusIs(string name, string status)
    {
        var subscription = Assert.Single(await world.Factory.SubscriptionsOfAsync(world.IdOf(name)));
        Assert.Equal(status, subscription.Status);
    }

    [Then(@"the active subscription of ""(.*)"" is for the plan ""(.*)""")]
    public async Task ThenTheActiveSubscriptionIsForThePlan(string name, string plan)
    {
        var active = Assert.Single(await world.Factory.SubscriptionsOfAsync(world.IdOf(name)),
            item => item.Status == "Active");
        Assert.Equal(SubscriptionsWorld.PlanIds[plan], active.PlanId);
    }

    [Then(@"the subscription for the plan ""(.*)"" of ""(.*)"" is closed")]
    public async Task ThenTheSubscriptionIsClosed(string plan, string name)
    {
        var closed = Assert.Single(await world.Factory.SubscriptionsOfAsync(world.IdOf(name)),
            item => item.PlanId == SubscriptionsWorld.PlanIds[plan]);
        Assert.Equal("Inactive", closed.Status);
        Assert.NotNull(closed.EndsAt);
    }

    // ----- helpers

    private HttpClient ClientWithToken(string token)
    {
        var client = world.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> OpenCheckoutAsync(string name, int planId)
    {
        var response = await world.ClientOf(name).PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessionId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;
        world.SessionIds[name] = sessionId;
        return sessionId;
    }

    private async Task StartCheckoutAsync(string name, int planId)
    {
        var response = await world.ClientOf(name).PostAsJsonAsync("/api/v1/subscriptions/stripe-checkout", new { planId });
        await world.SetResponseAsync(response);
    }

    private async Task ConfirmAsync(string caller, string sessionId)
    {
        await world.SetResponseAsync(await world.ClientOf(caller)
            .GetAsync($"/api/v1/subscriptions/stripe-checkout/{sessionId}/confirm"));
    }

    private async Task PostNotificationAsync(string payload, string? signature)
    {
        await world.SetResponseAsync(await world.Factory.PostWebhookAsync(payload, signature));
    }
}
