using System.Security.Claims;
using Anitec.Subscriptions.Shared.Domain.Model;

namespace Anitec.Subscriptions.Interfaces.Rest;

/// <summary>
///     Reads the identity of the caller from the claims of the validated token. The service never asks the
///     core for the user: the token is the only source, and nothing supplied by the client is trusted.
/// </summary>
public static class ClaimsPrincipalCallerExtensions
{
    /// <summary>
    ///     Gets the identity of the caller, or <c>null</c> when the token has no valid user id or role.
    /// </summary>
    public static CallerContext? GetCaller(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.Sid);
        var role = principal.FindFirstValue(ClaimTypes.Role);
        return int.TryParse(id, out var userId) && !string.IsNullOrWhiteSpace(role)
            ? new CallerContext(userId, role)
            : null;
    }
}
