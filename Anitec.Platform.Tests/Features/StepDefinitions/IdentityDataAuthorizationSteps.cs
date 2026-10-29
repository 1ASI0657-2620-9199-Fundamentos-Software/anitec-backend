using Anitec.Platform.Clients.Application.Internal.QueryServices;
using Anitec.Platform.Iam.Application.Internal.QueryServices;
using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Model.Queries;
using Anitec.Platform.Profiles.Application.Internal.QueryServices;
using Anitec.Platform.Profiles.Domain.Model.Aggregates;
using Anitec.Platform.Profiles.Domain.Model.Commands;
using Anitec.Platform.Profiles.Domain.Model.Queries;
using Anitec.Platform.Shared.Domain.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class IdentityDataAuthorizationWorld
{
    public InMemoryUserRepository Users { get; } = new();
    public InMemoryProfileRepository Profiles { get; } = new();
    public InMemoryVeterinarianClientRepository Relationships { get; } = new();
    public List<string> LastUserNames { get; set; } = [];
    public List<string> LastProfileNames { get; set; } = [];
    public bool? LastFound { get; set; }

    public UserQueryService CreateUserQueryService()
    {
        return new UserQueryService(Users, new VeterinarianClientQueryService(Relationships));
    }

    public ProfileQueryService CreateProfileQueryService()
    {
        return new ProfileQueryService(Profiles);
    }
}

[Binding]
public class IdentityDataAuthorizationSteps(IdentityDataAuthorizationWorld world)
{
    private static string[] Split(string names)
    {
        return names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static CreateProfileCommand ProfileCommand(string fullName, int? ownerId)
    {
        var parts = fullName.Split(' ', 2);
        return new CreateProfileCommand(parts[0], parts.Length > 1 ? parts[1] : "-", "profile@example.com", "Main St", "1",
            "Lima", "15001", "PE", ownerId);
    }

    [Given(@"the user ""(.*)"" with id (\d+) and role ""(.*)""")]
    public void GivenAUser(string name, int id, string role)
    {
        world.Users.Items.Add(new User(name.ToLowerInvariant(), "hash", name, role).WithId(id));
    }

    [Given(@"veterinarian (\d+) has the ""(.*)"" connection with rancher (\d+)")]
    public void GivenTheVeterinarianHasALink(int veterinarianId, string status, int rancherId)
    {
        world.Relationships.Seed(veterinarianId, rancherId, status);
    }

    [Given(@"user (\d+) created the profile ""(.*)""")]
    public async Task GivenUserCreatedAProfile(int ownerId, string fullName)
    {
        await world.Profiles.AddAsync(new Profile(ProfileCommand(fullName, ownerId)));
    }

    [Given(@"a profile ""(.*)"" exists without an owner")]
    public async Task GivenAProfileWithoutOwner(string fullName)
    {
        await world.Profiles.AddAsync(new Profile(ProfileCommand(fullName, null)));
    }

    [When(@"user (\d+) with role ""(.*)"" lists the users")]
    public async Task WhenUserListsTheUsers(int userId, string role)
    {
        var users = await world.CreateUserQueryService()
            .Handle(new GetUsersForCallerQuery(new CallerContext(userId, role)), CancellationToken.None);
        world.LastUserNames = users.Select(user => user.FullName).ToList();
    }

    [When(@"user (\d+) with role ""(.*)"" asks for the user (\d+)")]
    public async Task WhenUserAsksForAUser(int userId, string role, int targetId)
    {
        world.LastFound = await world.CreateUserQueryService()
            .Handle(new GetUserByIdForCallerQuery(targetId, new CallerContext(userId, role)), CancellationToken.None) is not null;
    }

    [When(@"user (\d+) with role ""(.*)"" lists the profiles")]
    public async Task WhenUserListsTheProfiles(int userId, string role)
    {
        var profiles = await world.CreateProfileQueryService()
            .Handle(new GetProfilesForCallerQuery(new CallerContext(userId, role)), CancellationToken.None);
        world.LastProfileNames = profiles.Select(profile => profile.FullName).ToList();
    }

    [When(@"user (\d+) with role ""(.*)"" asks for the profile ""(.*)""")]
    public async Task WhenUserAsksForAProfile(int userId, string role, string fullName)
    {
        var profile = world.Profiles.Items.Single(item => item.FullName == fullName);
        world.LastFound = await world.CreateProfileQueryService()
            .Handle(new GetProfileByIdForCallerQuery(profile.Id, new CallerContext(userId, role)), CancellationToken.None) is not null;
    }

    [Then(@"the users returned are ""(.*)""")]
    public void ThenTheUsersReturnedAre(string names)
    {
        Assert.Equal(Split(names).OrderBy(name => name), world.LastUserNames.OrderBy(name => name));
    }

    [Then(@"the profiles returned are ""(.*)""")]
    public void ThenTheProfilesReturnedAre(string names)
    {
        Assert.Equal(Split(names).OrderBy(name => name), world.LastProfileNames.OrderBy(name => name));
    }

    [Then(@"^the user is (found|not found)$")]
    public void ThenTheUserIs(string result)
    {
        Assert.Equal(result == "found", world.LastFound);
    }

    [Then("the profile is found")]
    public void ThenTheProfileIsFound()
    {
        Assert.True(world.LastFound);
    }

    [Then("the profile is not found")]
    public void ThenTheProfileIsNotFound()
    {
        Assert.False(world.LastFound);
    }
}
