using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Anitec.Subscriptions.Tests.Support;

/// <summary>
///     Issues tokens the way the core does (same claims and the same signing algorithm), so the tests exercise
///     the validation of the Subscriptions service with the real token format.
/// </summary>
public static class TokenFactory
{
    public static string Create(int userId, string role, string? secret = null, TimeSpan? lifetime = null)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Sid, userId.ToString()),
                new Claim(ClaimTypes.Name, $"user{userId}"),
                new Claim(ClaimTypes.Role, role)
            }),
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromDays(7)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secret ?? SubscriptionsApiFactory.TokenSecret)),
                SecurityAlgorithms.HmacSha256Signature)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
