using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Anitec.Gateway.Tests.Support;
using Reqnroll;

namespace Anitec.Gateway.Tests.Features.StepDefinitions;

/// <summary>
///     State of one scenario. The gateway and its fake services are the ones of the running xUnit collection
///     (the feature files carry the tag @xunit:collection(gateway)), so the scenarios never run at the same time
///     as the other gateway tests, which share the same fake services.
/// </summary>
public class GatewayWorld
{
    public GatewayFixture Fixture => GatewayFixture.Current;
    public HttpResponseMessage? Response { get; set; }
    public List<HttpResponseMessage> Responses { get; } = [];
    public JsonElement Body { get; set; }

    public IEnumerable<RecordedRequest> CoreRequests => Fixture.Api1.Requests.Concat(Fixture.Api2.Requests);

    public FakeService Replica(string name)
    {
        return name switch
        {
            "api1" => Fixture.Api1,
            "api2" => Fixture.Api2,
            _ => throw new ArgumentException($"Unknown replica {name}")
        };
    }

    public async Task SendAsync(HttpRequestMessage request)
    {
        var response = await Fixture.Client.SendAsync(request);
        Response = response;
        Responses.Add(response);
        var media = response.Content.Headers.ContentType?.MediaType ?? "";
        Body = media.Contains("json")
            ? JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone()
            : default;
    }

    public Task GetAsync(string path, Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        configure?.Invoke(request);
        return SendAsync(request);
    }
}

[Binding]
public class GatewaySteps(GatewayWorld world)
{
    [AfterScenario]
    public async Task LeaveTheServicesRunning()
    {
        await world.Fixture.ResetAsync();
    }

    // ----- Given: the system

    [Given("the gateway is running with two replicas of the core and the Subscriptions service")]
    public async Task GivenTheGatewayIsRunning()
    {
        await world.Fixture.ResetAsync();
    }

    [Given("the Subscriptions service stops")]
    public async Task GivenSubscriptionsStops()
    {
        await world.Fixture.Subscriptions.StopAsync();
    }

    [Given("the Subscriptions service reports itself as unhealthy")]
    public void GivenSubscriptionsReportsItselfUnhealthy()
    {
        world.Fixture.Subscriptions.ReportUnhealthy = true;
    }

    [Given(@"the gateway reports the service ""([^""]*)"" as ""([^""]*)""")]
    public async Task GivenTheGatewayReportsAService(string service, string health)
    {
        await GatewayFixture.WaitUntilAsync(async () =>
            (await world.Fixture.StatusAsync()).Contains((service, service, health)));
    }

    [Given(@"the replica ""([^""]*)"" of the core stops")]
    public async Task GivenAReplicaStops(string name)
    {
        await world.Replica(name).StopAsync();
    }

    [When("the Subscriptions service starts again")]
    public async Task WhenSubscriptionsStartsAgain()
    {
        await world.Fixture.Subscriptions.StartAsync();
    }

    [When(@"the replica ""([^""]*)"" starts again")]
    public async Task WhenAReplicaStartsAgain(string name)
    {
        await world.Replica(name).StartAsync();
    }

    // ----- When / Given: requests

    [When(@"a client sends GET ""([^""]*)""")]
    public async Task WhenAClientSendsGet(string path)
    {
        await world.GetAsync(path);
    }

    [Given(@"a client sends GET ""([^""]*)"" (\d+) times")]
    [When(@"a client sends GET ""([^""]*)"" (\d+) times")]
    public async Task WhenAClientSendsGetSeveralTimes(string path, int times)
    {
        for (var i = 0; i < times; i++) await world.GetAsync(path);
    }

    [Given(@"a client sends GET ""([^""]*)"" once")]
    [When(@"a client sends GET ""([^""]*)"" once")]
    public async Task GivenAClientSendsGetOnce(string path)
    {
        await world.GetAsync(path);
    }

    [When(@"a client sends GET ""([^""]*)"" with the correlation id ""([^""]*)""")]
    public async Task WhenAClientSendsGetWithACorrelationId(string path, string id)
    {
        await world.GetAsync(path, request => request.Headers.TryAddWithoutValidation("X-Correlation-Id", id));
    }

    [When(@"a client sends GET ""([^""]*)"" with the token ""([^""]*)""")]
    public async Task WhenAClientSendsGetWithAToken(string path, string token)
    {
        await world.GetAsync(path, request => request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}"));
    }

    private const string StripeEventBody = "{\"id\":\"evt_1\"}";

    [When(@"a client posts a Stripe event with the signature ""([^""]*)"" to ""([^""]*)""")]
    public async Task WhenAClientPostsAWebhook(string signature, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(StripeEventBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", signature);
        await world.SendAsync(request);
    }

    // ----- Then: routing

    [Then(@"the response status is (\d+)")]
    public void ThenTheResponseStatusIs(int status)
    {
        Assert.Equal(status, (int)world.Response!.StatusCode);
    }

    [Then(@"all the answers have status (\d+)")]
    public void ThenAllTheAnswersHaveStatus(int status)
    {
        Assert.All(world.Responses, response => Assert.Equal(status, (int)response.StatusCode));
    }

    [Then(@"the request was served by the ""([^""]*)"" service")]
    public void ThenServedBy(string service)
    {
        var servedBy = world.Response!.Headers.GetValues("X-Served-By").Single();
        if (service == "core") Assert.StartsWith("api", servedBy);
        else Assert.Equal("subscriptions", servedBy);
    }

    [Then("the core received nothing")]
    public void ThenTheCoreReceivedNothing()
    {
        Assert.Empty(world.CoreRequests);
    }

    [Then("the Subscriptions service received nothing")]
    public void ThenSubscriptionsReceivedNothing()
    {
        Assert.Empty(world.Fixture.Subscriptions.Requests);
    }

    [Then("both replicas of the core received requests")]
    public void ThenBothReplicasReceivedRequests()
    {
        Assert.NotEmpty(world.Fixture.Api1.Requests);
        Assert.NotEmpty(world.Fixture.Api2.Requests);
    }

    [Then("the Subscriptions service received that body and that signature unchanged")]
    public void ThenTheWebhookArrivedUntouched()
    {
        var received = Assert.Single(world.Fixture.Subscriptions.Requests);
        Assert.Equal("POST", received.Method);
        Assert.Equal("/api/v1/subscriptions/stripe/webhook", received.PathAndQuery);
        Assert.Equal(StripeEventBody, received.Body);
        Assert.Equal("t=1,v1=abc", received.Headers["Stripe-Signature"]);
        Assert.Empty(world.CoreRequests);
    }

    [Then(@"the core received the header Authorization ""([^""]*)""")]
    public void ThenTheCoreReceivedTheAuthorization(string value)
    {
        var received = Assert.Single(world.CoreRequests);
        Assert.Equal(value, received.Headers["Authorization"]);
    }

    // ----- Then: contract

    [Then("the answer carries a correlation id")]
    public void ThenTheAnswerCarriesACorrelationId()
    {
        Assert.False(string.IsNullOrWhiteSpace(world.Response!.Headers.GetValues("X-Correlation-Id").Single()));
    }

    [Then(@"the answer carries the correlation id ""([^""]*)""")]
    public void ThenTheAnswerCarriesTheCorrelationId(string id)
    {
        Assert.Equal(id, world.Response!.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Then(@"the answer carries a correlation id different from ""([^""]*)""")]
    public void ThenTheAnswerCarriesADifferentCorrelationId(string sent)
    {
        var received = world.Response!.Headers.GetValues("X-Correlation-Id").Single();
        Assert.NotEqual(sent, received);
        Assert.Matches("^[A-Za-z0-9._-]{1,64}$", received);
    }

    [Then("the service received that same correlation id")]
    public void ThenTheServiceReceivedTheSameCorrelationId()
    {
        var expected = world.Response!.Headers.GetValues("X-Correlation-Id").Single();
        var received = Assert.Single(world.CoreRequests.Concat(world.Fixture.Subscriptions.Requests));
        Assert.Equal(expected, received.Headers["X-Correlation-Id"]);
    }

    [Then(@"the answer is the documented error for the unsupported version ""([^""]*)""")]
    public void ThenTheAnswerIsTheDocumentedError(string version)
    {
        var response = world.Response!;
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("v1", response.Headers.GetValues("api-supported-versions").Single());
        Assert.Equal("Unsupported API version", world.Body.GetProperty("title").GetString());
        Assert.Equal(version, world.Body.GetProperty("requestedVersion").GetString());
        Assert.Equal("v1", world.Body.GetProperty("supportedVersions")[0].GetString());
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(),
            world.Body.GetProperty("correlationId").GetString());
    }

    [Then(@"the answer is the one of the ""([^""]*)"" service")]
    public void ThenTheAnswerIsTheOneOfTheService(string service)
    {
        Assert.Equal("Payments unavailable", world.Body.GetProperty("title").GetString());
        Assert.Equal(service, world.Body.GetProperty("from").GetString());
    }

    // ----- Then: isolation and recovery

    [Then(@"every answer is a problem document that names the ""([^""]*)"" service")]
    public async Task ThenEveryAnswerIsAProblemDocument(string service)
    {
        Assert.NotEmpty(world.Responses);
        foreach (var response in world.Responses)
        {
            Assert.True(response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable,
                $"Unexpected status {response.StatusCode}");
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(service, body.GetProperty("service").GetString());
            Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(),
                body.GetProperty("correlationId").GetString());
        }
    }

    [Then(@"(\d+) requests to ""([^""]*)"" are all answered with status 200")]
    public async Task ThenRequestsAreAllAnswered(int count, string path)
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < count; i++) statuses.Add((await world.Fixture.Client.GetAsync(path)).StatusCode);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    [Then("the plans route answers 200 again")]
    public async Task ThenThePlansRouteAnswersAgain()
    {
        await GatewayFixture.WaitUntilAsync(async () =>
            (await world.Fixture.Client.GetAsync("/api/v1/subscription-plans")).StatusCode == HttpStatusCode.OK);
    }

    [Then(@"at most (\d+) of those requests failed")]
    public void ThenAtMostFailed(int maximum)
    {
        var failures = world.Responses.Count(response => response.StatusCode != HttpStatusCode.OK);
        Assert.True(failures <= maximum, $"{failures} requests failed before the replica left the rotation");
    }

    [Then(@"(\d+) more requests to ""([^""]*)"" are all answered by the replica ""([^""]*)""")]
    public async Task ThenAnsweredByTheReplica(int count, string path, string replica)
    {
        var service = world.Replica(replica);
        service.Clear();
        for (var i = 0; i < count; i++)
            Assert.Equal(HttpStatusCode.OK, (await world.Fixture.Client.GetAsync(path)).StatusCode);
        Assert.Equal(count, service.Requests.Count);
    }

    [Then(@"the gateway reports the replica ""([^""]*)"" as ""([^""]*)"" and ""([^""]*)"" as ""([^""]*)""")]
    public async Task ThenTheGatewayReportsTwoReplicas(string first, string firstHealth, string second, string secondHealth)
    {
        var status = await world.Fixture.StatusAsync();
        Assert.Contains(("core", first, firstHealth), status);
        Assert.Contains(("core", second, secondHealth), status);
    }

    [Then(@"the gateway reports the replica ""([^""]*)"" as ""([^""]*)""")]
    public async Task ThenTheGatewayReportsAReplica(string replica, string health)
    {
        await GatewayFixture.WaitUntilAsync(async () =>
            (await world.Fixture.StatusAsync()).Contains(("core", replica, health)));
    }

    [Then("both replicas receive traffic again without any failed request")]
    public async Task ThenBothReplicasReceiveTraffic()
    {
        world.Fixture.Api1.Clear();
        world.Fixture.Api2.Clear();
        await GatewayFixture.WaitUntilAsync(async () =>
        {
            Assert.Equal(HttpStatusCode.OK, (await world.Fixture.Client.GetAsync("/api/v1/animals")).StatusCode);
            return world.Fixture.Api1.Requests.Count > 0 && world.Fixture.Api2.Requests.Count > 0;
        });
    }
}
