using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace SocialTennis.Api.Features.Auth;

public static class GetCurrentUser
{
    public static Results<Ok<MeResponse>, UnauthorizedHttpResult> HandleAsync(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);
        return id is null || email is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new MeResponse(Guid.Parse(id), email));
    }
}
