using Anitec.Platform.Iam.Application.Internal.CommandServices;
using Anitec.Platform.Iam.Application.Internal.OutboundServices;
using Anitec.Platform.Iam.Domain.Model;
using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Domain.Model.Commands;
using Anitec.Platform.Iam.Domain.Repositories;
using Anitec.Platform.Iam.Infrastructure.Hashing.BCrypt.Services;
using Anitec.Platform.Shared.Domain.Repositories;
using Anitec.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Anitec.Platform.Tests.Iam;

public class UserCommandServiceTests
{
    private readonly HashingService _hashing = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();

    public UserCommandServiceTests()
    {
        _tokens.GenerateToken(Arg.Any<User>()).Returns(call => $"token-for-{call.Arg<User>().Username}");
    }

    private UserCommandService CreateService()
    {
        return new UserCommandService(_users, _tokens, _hashing, _unitOfWork, new FakeLocalizer());
    }

    private void GivenRegisteredUser(string username, string password, string role = "Rancher")
    {
        _users.Items.Add(new User(username, _hashing.HashPassword(password), username, role));
    }

    [Fact]
    public async Task SignIn_WithValidCredentials_ReturnsUserAndToken()
    {
        GivenRegisteredUser("ganadero", "anitec123");

        var result = await CreateService().Handle(new SignInCommand("ganadero", "anitec123"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ganadero", result.Value.user.Username);
        Assert.Equal("token-for-ganadero", result.Value.token);
    }

    [Fact]
    public async Task SignIn_WithWrongPassword_FailsWithInvalidCredentials_AndIssuesNoToken()
    {
        GivenRegisteredUser("ganadero", "anitec123");

        var result = await CreateService().Handle(new SignInCommand("ganadero", "incorrecta"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IamError.InvalidCredentials, result.Error);
        _tokens.DidNotReceive().GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task SignIn_WithUnknownUser_FailsWithTheSameErrorAsAWrongPassword()
    {
        var result = await CreateService().Handle(new SignInCommand("fantasma", "anitec123"), CancellationToken.None);

        Assert.Equal(IamError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task SignUp_StoresAHashedPassword_NotThePlainText()
    {
        var result = await CreateService().Handle(
            new SignUpCommand("nuevo", "anitec123", "Nuevo Usuario", "Rancher"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_users.Items);
        Assert.NotEqual("anitec123", stored.PasswordHash);
        Assert.True(_hashing.VerifyPassword("anitec123", stored.PasswordHash));
    }

    [Fact]
    public async Task SignUp_WithATakenUsername_Fails()
    {
        GivenRegisteredUser("ganadero", "anitec123");

        var result = await CreateService().Handle(
            new SignUpCommand("ganadero", "otra", "Otro", "Rancher"), CancellationToken.None);

        Assert.Equal(IamError.UsernameAlreadyTaken, result.Error);
        Assert.Single(_users.Items);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task SignUp_WithAnUnsupportedRole_Fails(string role)
    {
        var result = await CreateService().Handle(
            new SignUpCommand("nuevo", "anitec123", "Nuevo", role), CancellationToken.None);

        Assert.Equal(IamError.InvalidRole, result.Error);
        Assert.Empty(_users.Items);
    }

    [Theory]
    [InlineData("rancher", "Rancher")]
    [InlineData("VETERINARIAN", "Veterinarian")]
    public async Task SignUp_NormalizesTheRoleCasing(string requested, string expected)
    {
        await CreateService().Handle(new SignUpCommand("nuevo", "anitec123", "Nuevo", requested),
            CancellationToken.None);

        Assert.Equal(expected, Assert.Single(_users.Items).Role);
    }

    [Fact]
    public async Task SignUp_WhenTheUniqueIndexRejectsAConcurrentDuplicate_FailsWithUsernameAlreadyTaken()
    {
        // Two sign-ups pass the duplicate check at the same time; the unique index rejects the second one.
        var racingUsers = new RacingUserRepository();
        var service = new UserCommandService(racingUsers, _tokens, _hashing, new RejectingUnitOfWork(),
            new FakeLocalizer());

        var result = await service.Handle(
            new SignUpCommand("repetido", "anitec123", "Repetido", "Rancher"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(IamError.UsernameAlreadyTaken, result.Error);
    }

    [Fact]
    public async Task SignUp_WhenTheDatabaseFailsForAnotherReason_FailsWithDatabaseError()
    {
        // The username is really free: the repository never reports it as taken, even after the failed save.
        var freeUsers = Substitute.For<IUserRepository>();
        freeUsers.ExistsByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var service = new UserCommandService(freeUsers, _tokens, _hashing, new RejectingUnitOfWork(),
            new FakeLocalizer());

        var result = await service.Handle(
            new SignUpCommand("nuevo", "anitec123", "Nuevo", "Rancher"), CancellationToken.None);

        Assert.Equal(IamError.DatabaseError, result.Error);
    }

    /// <summary>Reports the username as free on the first check and as taken afterwards.</summary>
    private sealed class RacingUserRepository : InMemoryUserRepository, IUserRepository
    {
        private int _checks;

        public new Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            return Task.FromResult(++_checks > 1);
        }
    }

    private sealed class RejectingUnitOfWork : IUnitOfWork
    {
        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException("Duplicate entry for key 'i_x_users_username'.");
        }
    }
}
