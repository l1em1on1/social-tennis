using System.Security.Claims;

namespace SocialTennis.Api.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in User's id. Only for endpoints behind the fallback policy
    /// (ADR-0017), where the session scheme has always set the claim.
    /// </summary>
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The request has no signed-in User."));
}
