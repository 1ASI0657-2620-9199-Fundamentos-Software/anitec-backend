using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Livestock.Application;
using Anitec.Platform.Livestock.Application.Internal.OutboundServices;
using Anitec.Platform.Livestock.Application.Internal.QueryServices;
using Anitec.Platform.Livestock.Domain.Model.Entities;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class AnimalPlanLimitWorld
{
    public InMemoryAnimalRepository Animals { get; } = new();
    public InMemoryHerdRepository Herds { get; } = new();
    public InMemoryVeterinarianClientRepository Relationships { get; } = new();
    public Dictionary<int, ActivePlanInfo> Plans { get; } = [];
    public PlanLookupStatus Mode { get; set; } = PlanLookupStatus.NoActivePlan;
    public PlanLimitDecision? Decision { get; set; }
    public string? TokenSeenByTheClient { get; set; }
}

[Binding]
public class AnimalPlanLimitSteps(AnimalPlanLimitWorld world)
{
    [Given(@"rancher (\d+) has (\d+) animals?")]
    public async Task GivenTheRancherHasAnimals(int rancherId, int count)
    {
        var herd = world.Herds.Items.FirstOrDefault(item => item.OwnerId == rancherId)
                   ?? world.Herds.Seed($"Herd {rancherId}", rancherId);
        for (var i = 0; i < count; i++)
            await world.Animals.AddAsync(new Animal { Tag = $"R{rancherId}-{i}", HerdId = herd.Id });
    }

    [Given(@"rancher (\d+) has no active plan")]
    public void GivenNoPlan(int rancherId)
    {
        world.Mode = PlanLookupStatus.NoActivePlan;
    }

    [Given(@"rancher (\d+) has the plan ""(.*)"" with a limit of (\d+) animals")]
    public void GivenThePlan(int rancherId, string name, int limit)
    {
        world.Mode = PlanLookupStatus.Found;
        world.Plans[rancherId] = new ActivePlanInfo(1, name, limit);
    }

    [Given(@"the Subscriptions service is not available")]
    public void GivenUnavailable()
    {
        world.Mode = PlanLookupStatus.Unavailable;
    }

    [Given(@"the address of the Subscriptions service is not configured")]
    public void GivenNotConfigured()
    {
        world.Mode = PlanLookupStatus.NotConfigured;
    }

    [When(@"rancher (\d+) wants to add (\d+) animals?")]
    public async Task WhenTheRancherWantsToAdd(int rancherId, int count)
    {
        world.Decision = await CheckAsync(new CallerContext(rancherId, CallerContext.RancherRole), "token-of-the-rancher",
            count);
    }

    [When(@"rancher (\d+) without a token wants to add (\d+) animals?")]
    public async Task WhenTheRancherWithoutATokenWantsToAdd(int rancherId, int count)
    {
        world.Decision = await CheckAsync(new CallerContext(rancherId, CallerContext.RancherRole), null, count);
    }

    [When(@"veterinarian (\d+) wants to add (\d+) animals?")]
    public async Task WhenTheVeterinarianWantsToAdd(int id, int count)
    {
        world.Decision = await CheckAsync(new CallerContext(id, CallerContext.VeterinarianRole), "token", count);
    }

    private Task<PlanLimitDecision> CheckAsync(CallerContext caller, string? token, int count)
    {
        var client = Substitute.For<ISubscriptionPlanClient>();
        client.GetActivePlanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            world.TokenSeenByTheClient = call.Arg<string>();
            return Task.FromResult(world.Mode switch
            {
                PlanLookupStatus.Found => new PlanLookupResult(PlanLookupStatus.Found,
                    world.Plans.GetValueOrDefault(caller.Id)),
                PlanLookupStatus.NoActivePlan => PlanLookupResult.NoActivePlan,
                PlanLookupStatus.NotConfigured => PlanLookupResult.NotConfigured,
                _ => PlanLookupResult.Unavailable
            });
        });
        var animals = new AnimalQueryService(world.Animals, world.Herds,
            new VeterinarianClientQueryService(world.Relationships));
        return new AnimalPlanLimitService(client, animals, NullLogger<AnimalPlanLimitService>.Instance)
            .CheckAsync(caller, token, count, CancellationToken.None);
    }

    [Then(@"the animals are allowed because there is no plan")]
    public void ThenAllowedNoPlan()
    {
        AssertAllowed(PlanLimitReason.NoPlan);
    }

    [Then(@"the animals are allowed within the limit")]
    public void ThenAllowedWithinTheLimit()
    {
        AssertAllowed(PlanLimitReason.WithinLimit);
        Assert.Equal("token-of-the-rancher", world.TokenSeenByTheClient);
    }

    [Then(@"the animals are allowed because the plan could not be checked")]
    public void ThenAllowedUnavailable()
    {
        AssertAllowed(PlanLimitReason.PlanServiceUnavailable);
    }

    [Then(@"the animals are allowed and the check does not apply")]
    public void ThenAllowedNotApplicable()
    {
        AssertAllowed(PlanLimitReason.NotApplicable);
    }

    [Then(@"the animals are refused because the limit of (\d+) is reached with (\d+) animals")]
    public void ThenRefused(int limit, int current)
    {
        Assert.NotNull(world.Decision);
        Assert.False(world.Decision.Allowed);
        Assert.Equal(PlanLimitReason.LimitReached, world.Decision.Reason);
        Assert.Equal(limit, world.Decision.MaxAnimals);
        Assert.Equal(current, world.Decision.CurrentAnimals);
        Assert.Equal("Ganadero Basico", world.Decision.PlanName);
    }

    private void AssertAllowed(PlanLimitReason reason)
    {
        Assert.NotNull(world.Decision);
        Assert.True(world.Decision.Allowed);
        Assert.Equal(reason, world.Decision.Reason);
    }
}
