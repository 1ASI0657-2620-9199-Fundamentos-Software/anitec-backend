using Anitec.Platform.Iam.Application.Internal.CommandServices;
using Anitec.Platform.Iam.Application.Internal.OutboundServices;
using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Model.Commands;
using Anitec.Platform.Iam.Infrastructure.Hashing.BCrypt.Services;
using Anitec.Platform.Shared.Application.Model;
using Anitec.Platform.Tests.Support;
using Reqnroll;

namespace Anitec.Platform.Tests.Features.StepDefinitions;

public class FakeTokenService : ITokenService
{
    public List<string> IssuedTokens { get; } = [];

    public string GenerateToken(User user)
    {
        var token = $"token-for-{user.Username}";
        IssuedTokens.Add(token);
        return token;
    }

    public Task<int?> ValidateToken(string token)
    {
        return Task.FromResult<int?>(IssuedTokens.Contains(token) ? 1 : null);
    }
}

public class AuthenticationWorld
{
    public HashingService Hashing { get; } = new();
    public InMemoryUserRepository Users { get; } = new();
    public FakeTokenService Tokens { get; } = new();
    public Result<(User user, string token)>? SignInResult { get; set; }
    public Result? SignUpResult { get; set; }

    public UserCommandService CreateService()
    {
        return new UserCommandService(Users, Tokens, Hashing, new CountingUnitOfWork(), new FakeLocalizer());
    }
}

[Binding]
public class AuthenticationSteps(AuthenticationWorld world)
{
    [Given("the following registered users")]
    public void GivenTheFollowingRegisteredUsers(DataTable table)
    {
        foreach (var row in table.Rows)
            world.Users.Items.Add(new User(row["username"], world.Hashing.HashPassword(row["password"]),
                row["username"], row["role"]));
    }

    [Given(@"I sign up as ""(.*)"" with password ""(.*)"", full name ""(.*)"" and role ""(.*)""")]
    [When(@"I sign up as ""(.*)"" with password ""(.*)"" and full name ""(.*)"" and role ""(.*)""")]
    public async Task WhenISignUp(string username, string password, string fullName, string role)
    {
        world.SignUpResult = await world.CreateService()
            .Handle(new SignUpCommand(username, password, fullName, role), CancellationToken.None);
    }

    [When(@"I sign in with username ""(.*)"" and password ""(.*)""")]
    public async Task WhenISignIn(string username, string password)
    {
        world.SignInResult = await world.CreateService()
            .Handle(new SignInCommand(username, password), CancellationToken.None);
    }

    [Then("the sign in succeeds")]
    public void ThenTheSignInSucceeds()
    {
        Assert.NotNull(world.SignInResult);
        Assert.True(world.SignInResult.IsSuccess);
    }

    [Then(@"the sign in fails with the error ""(.*)""")]
    public void ThenTheSignInFailsWithTheError(string error)
    {
        Assert.NotNull(world.SignInResult);
        Assert.True(world.SignInResult.IsFailure);
        Assert.Equal(error, world.SignInResult.Error?.ToString());
    }

    [Then(@"a token is issued for the user ""(.*)""")]
    public void ThenATokenIsIssuedForTheUser(string username)
    {
        Assert.NotNull(world.SignInResult);
        Assert.Equal($"token-for-{username}", world.SignInResult.Value.token);
        Assert.Contains($"token-for-{username}", world.Tokens.IssuedTokens);
    }

    [Then("no token is issued")]
    public void ThenNoTokenIsIssued()
    {
        Assert.Empty(world.Tokens.IssuedTokens);
    }

    [Then("the sign up succeeds")]
    public void ThenTheSignUpSucceeds()
    {
        Assert.NotNull(world.SignUpResult);
        Assert.True(world.SignUpResult.IsSuccess);
    }

    [Then(@"the sign up fails with the error ""(.*)""")]
    public void ThenTheSignUpFailsWithTheError(string error)
    {
        Assert.NotNull(world.SignUpResult);
        Assert.True(world.SignUpResult.IsFailure);
        Assert.Equal(error, world.SignUpResult.Error?.ToString());
    }
}
