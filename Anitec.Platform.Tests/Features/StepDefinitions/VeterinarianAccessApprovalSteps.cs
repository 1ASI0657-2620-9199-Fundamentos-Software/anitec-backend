using Anitec.Platform.Clients.Application.Internal.CommandServices;
using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Clients.Domain.Model.Commands;
using Anitec.Platform.Clients.Domain.Model.Entities;
using Anitec.Platform.Clients.Domain.Model.Queries;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class VeterinarianAccessWorld
{
    public InMemoryVeterinarianClientRepository Relationships { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public Result<VeterinarianClient>? LastResult { get; set; }

    public VeterinarianClientCommandService CreateCommandService()
    {
        return new VeterinarianClientCommandService(Relationships, UnitOfWork);
    }

    public VeterinarianClient Stored(int veterinarianId, int rancherId)
    {
        return Relationships.Items.Single(item => item.VeterinarianId == veterinarianId && item.RancherId == rancherId);
    }
}

[Binding]
public class VeterinarianAccessApprovalSteps(VeterinarianAccessWorld world)
{
    private int _lastVeterinarianId;
    private int _lastRancherId;

    [Given(@"veterinarian (\d+) requested access to rancher (\d+)")]
    [When(@"veterinarian (\d+) requests access to rancher (\d+)")]
    public async Task WhenTheVeterinarianRequestsAccess(int veterinarianId, int rancherId)
    {
        _lastVeterinarianId = veterinarianId;
        _lastRancherId = rancherId;
        world.LastResult = await world.CreateCommandService()
            .Handle(new CreateVeterinarianClientCommand(veterinarianId, rancherId), CancellationToken.None);
    }

    [Given(@"rancher (\d+) approves the request of veterinarian (\d+)")]
    [When(@"rancher (\d+) approves the request of veterinarian (\d+)")]
    public async Task WhenTheRancherApproves(int rancherId, int veterinarianId)
    {
        _lastVeterinarianId = veterinarianId;
        _lastRancherId = rancherId;
        world.LastResult = await world.CreateCommandService()
            .Handle(new ApproveVeterinarianClientCommand(veterinarianId, rancherId), CancellationToken.None);
    }

    [Given(@"rancher (\d+) rejects the request of veterinarian (\d+)")]
    [When(@"rancher (\d+) rejects the request of veterinarian (\d+)")]
    public async Task WhenTheRancherRejects(int rancherId, int veterinarianId)
    {
        _lastVeterinarianId = veterinarianId;
        _lastRancherId = rancherId;
        world.LastResult = await world.CreateCommandService()
            .Handle(new RejectVeterinarianClientCommand(veterinarianId, rancherId), CancellationToken.None);
    }

    [Then(@"the request is saved with the status ""(.*)""")]
    [Then(@"the request has the status ""(.*)""")]
    public void ThenTheRequestHasTheStatus(string status)
    {
        Assert.Equal(status, world.Stored(_lastVeterinarianId, _lastRancherId).Status);
    }

    [Then("the request has no acceptance date")]
    public void ThenTheRequestHasNoAcceptanceDate()
    {
        Assert.Null(world.Stored(_lastVeterinarianId, _lastRancherId).AcceptedAt);
    }

    [Then("the request has an acceptance date")]
    public void ThenTheRequestHasAnAcceptanceDate()
    {
        Assert.NotNull(world.Stored(_lastVeterinarianId, _lastRancherId).AcceptedAt);
    }

    [Then(@"the operation fails with the error ""(.*)""")]
    public void ThenTheOperationFailsWithTheError(string error)
    {
        Assert.NotNull(world.LastResult);
        Assert.True(world.LastResult!.IsFailure);
        Assert.Equal(error, world.LastResult.Error!.ToString());
    }

    [Then(@"the rancher (\d+) data is not visible to veterinarian (\d+)")]
    public async Task ThenTheDataIsNotVisible(int rancherId, int veterinarianId)
    {
        Assert.DoesNotContain(rancherId, await AcceptedRancherIdsAsync(veterinarianId));
    }

    [Then(@"the rancher (\d+) data is visible to veterinarian (\d+)")]
    public async Task ThenTheDataIsVisible(int rancherId, int veterinarianId)
    {
        Assert.Contains(rancherId, await AcceptedRancherIdsAsync(veterinarianId));
    }

    private async Task<IReadOnlySet<int>> AcceptedRancherIdsAsync(int veterinarianId)
    {
        return await new VeterinarianClientQueryService(world.Relationships)
            .Handle(new GetAcceptedRancherIdsByVeterinarianIdQuery(veterinarianId), CancellationToken.None);
    }
}
