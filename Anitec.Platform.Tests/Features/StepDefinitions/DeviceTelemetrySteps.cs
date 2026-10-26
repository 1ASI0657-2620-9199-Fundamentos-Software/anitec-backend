using Anitec.Platform.Devices.Application.Internal.CommandServices;
using Anitec.Platform.Devices.Application.Internal.QueryServices;
using Anitec.Platform.Devices.Domain.Model.Commands;
using Anitec.Platform.Devices.Domain.Model.Entities;
using Anitec.Platform.Devices.Domain.Model.Queries;
using Anitec.Platform.Devices.Infrastructure.Pipeline;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class DeviceTelemetryWorld
{
    public InMemoryDeviceRepository Devices { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public Dictionary<string, string> Keys { get; } = new();
    public List<string> IssuedKeys { get; } = [];
    public string? LastIssuedKey { get; set; }
    public Result<string>? LastIssueResult { get; set; }
    public Device? Authenticated { get; set; }
    public AuthorizationFilterContext? FilterContext { get; set; }

    public DeviceCommandService CreateCommandService()
    {
        return new DeviceCommandService(Devices, UnitOfWork);
    }

    public DeviceQueryService CreateQueryService()
    {
        return new DeviceQueryService(Devices);
    }
}

[Binding]
public class DeviceTelemetrySteps(DeviceTelemetryWorld world)
{
    private Device Named(string name)
    {
        return world.Devices.Items.Single(device => device.Name == name);
    }

    [Given(@"a device named ""(.*)"" exists")]
    public async Task GivenADeviceExists(string name)
    {
        await world.Devices.AddAsync(new Device { Name = name, Type = "Sensor", SerialNumber = name, Status = "Online", HerdId = 1 });
    }

    [Given(@"an API key was issued for ""(.*)""")]
    [Given(@"a new API key is issued for ""(.*)""")]
    [When(@"an API key is issued for ""(.*)""")]
    public async Task WhenAnApiKeyIsIssued(string name)
    {
        var device = Named(name);
        var result = await world.CreateCommandService()
            .Handle(new IssueDeviceApiKeyCommand(device.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        world.LastIssueResult = result;
        world.LastIssuedKey = result.Value;
        world.Keys[name] = result.Value!;
        world.IssuedKeys.Add(result.Value!);
    }

    [When(@"an API key is issued for the device with id (\d+)")]
    public async Task WhenAnApiKeyIsIssuedForTheDeviceWithId(int deviceId)
    {
        world.LastIssueResult = await world.CreateCommandService()
            .Handle(new IssueDeviceApiKeyCommand(deviceId), CancellationToken.None);
    }

    [When(@"""(.*)"" authenticates with its key")]
    public async Task WhenTheDeviceAuthenticatesWithItsKey(string name)
    {
        await Authenticate(name, world.Keys[name]);
    }

    [When(@"""(.*)"" authenticates with its first key")]
    public async Task WhenTheDeviceAuthenticatesWithItsFirstKey(string name)
    {
        await Authenticate(name, world.IssuedKeys.First());
    }

    [When(@"""(.*)"" authenticates with the key ""(.*)""")]
    public async Task WhenTheDeviceAuthenticatesWithTheKey(string name, string key)
    {
        await Authenticate(name, key);
    }

    [When(@"""(.*)"" authenticates with the key of ""(.*)""")]
    public async Task WhenTheDeviceAuthenticatesWithTheKeyOf(string name, string otherName)
    {
        await Authenticate(name, world.Keys[otherName]);
    }

    [When(@"a telemetry request for ""(.*)"" carries its key in the ""(.*)"" header")]
    public async Task WhenARequestCarriesItsKey(string name, string header)
    {
        await RunFilter(Named(name).Id, header, world.Keys[name]);
    }

    [When(@"a telemetry request for ""(.*)"" carries no key")]
    public async Task WhenARequestCarriesNoKey(string name)
    {
        await RunFilter(Named(name).Id, null, null);
    }

    [When(@"a telemetry request for ""(.*)"" carries the key of ""(.*)""")]
    public async Task WhenARequestCarriesTheKeyOf(string name, string keyOwner)
    {
        await RunFilter(Named(name).Id, DeviceApiKeyFilter.HeaderName, world.Keys[keyOwner]);
    }

    [Then(@"the key starts with ""(.*)""")]
    public void ThenTheKeyStartsWith(string prefix)
    {
        Assert.StartsWith(prefix, world.LastIssuedKey);
    }

    [Then(@"the stored value for ""(.*)"" is a SHA-256 hash and not the key")]
    public void ThenTheStoredValueIsAHash(string name)
    {
        var stored = Named(name).ApiKeyHash;
        Assert.NotNull(stored);
        Assert.NotEqual(world.LastIssuedKey, stored);
        Assert.Equal(64, stored!.Length);
        Assert.Matches("^[0-9A-F]+$", stored);
    }

    [Then("the device is authenticated")]
    public void ThenTheDeviceIsAuthenticated()
    {
        Assert.NotNull(world.Authenticated);
    }

    [Then("the device is rejected")]
    public void ThenTheDeviceIsRejected()
    {
        Assert.Null(world.Authenticated);
    }

    [Then("issuing the key fails because the device does not exist")]
    public void ThenTheOperationFailsWithDeviceNotFound()
    {
        Assert.True(world.LastIssueResult!.IsFailure);
        Assert.Equal("DeviceNotFound", world.LastIssueResult.Error!.ToString());
    }

    [Then(@"the request is authorized as ""(.*)""")]
    public void ThenTheRequestIsAuthorizedAs(string name)
    {
        Assert.Null(world.FilterContext!.Result);
        var device = Assert.IsType<Device>(world.FilterContext.HttpContext.Items[DeviceApiKeyFilter.ItemKey]);
        Assert.Equal(name, device.Name);
    }

    [Then(@"the request is rejected with status (\d+)")]
    public void ThenTheRequestIsRejectedWithStatus(int status)
    {
        var result = Assert.IsAssignableFrom<IStatusCodeActionResult>(world.FilterContext!.Result);
        Assert.Equal(status, result.StatusCode);
        Assert.False(world.FilterContext.HttpContext.Items.ContainsKey(DeviceApiKeyFilter.ItemKey));
    }

    private async Task Authenticate(string name, string key)
    {
        world.Authenticated = await world.CreateQueryService()
            .Handle(new AuthenticateDeviceQuery(Named(name).Id, key), CancellationToken.None);
    }

    private async Task RunFilter(int deviceId, string? header, string? key)
    {
        var httpContext = new DefaultHttpContext();
        if (header is not null && key is not null) httpContext.Request.Headers[header] = key;

        var routeData = new RouteData();
        routeData.Values["deviceId"] = deviceId.ToString();
        var context = new AuthorizationFilterContext(
            new ActionContext(httpContext, routeData, new ActionDescriptor()),
            new List<IFilterMetadata>());

        await new DeviceApiKeyFilter(world.CreateQueryService()).OnAuthorizationAsync(context);
        world.FilterContext = context;
    }
}
