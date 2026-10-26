using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Anitec.Platform.Tests.Iam;

public class AuthorizeAttributeTests
{
    private static AuthorizationFilterContext ContextFor(User? user)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null) httpContext.Items["User"] = user;

        return new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());
    }

    [Fact]
    public void WhenThereIsNoAuthenticatedUser_AnswersUnauthorized()
    {
        var context = ContextFor(null);

        new AuthorizeAttribute("Rancher").OnAuthorization(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void WhenTheRoleIsNotAllowed_AnswersForbiddenWithStatus403()
    {
        var context = ContextFor(new User("pedro", "hash", "Pedro", "Veterinarian"));

        new AuthorizeAttribute("Rancher").OnAuthorization(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
    }

    [Theory]
    [InlineData("Rancher")]
    [InlineData("rancher")]
    public void WhenTheRoleIsAllowed_DoesNotSetAResult(string role)
    {
        var context = ContextFor(new User("ana", "hash", "Ana", role));

        new AuthorizeAttribute("Rancher", "Veterinarian").OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
