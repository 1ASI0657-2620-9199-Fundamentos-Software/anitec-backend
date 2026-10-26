using Anitec.Platform.Iam.Domain.Model.Aggregates;
using Anitec.Platform.Shared.Domain.Model;

namespace Anitec.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;

/// <summary>
///     Reads the authenticated user that <c>RequestAuthorizationMiddleware</c> stored in
///     <c>HttpContext.Items["User"]</c>.
/// </summary>
public static class HttpContextCallerExtensions
{
    /// <summary>
    ///     Gets the identity of the caller, or <c>null</c> when the request is not authenticated.
    /// </summary>
    public static CallerContext? GetCaller(this HttpContext context)
    {
        return context.Items["User"] is User user ? new CallerContext(user.Id, user.Role) : null;
    }
}
