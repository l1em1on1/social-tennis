using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Auth.Contracts;

namespace SocialTennis.Api.Features.Auth;

public static class GetCurrentUser
{
    public static async Task<Results<Ok<GetCurrentUserResponse>, UnauthorizedHttpResult>> HandleAsync(
        ClaimsPrincipal user, ManagerAccess access, CancellationToken cancellationToken)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);
        return id is null || email is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new GetCurrentUserResponse(
                Guid.Parse(id),
                email,
                user.IsInRole(SessionRoles.Admin),
                await access.RunsAnythingAsync(user, cancellationToken)));
    }
}
