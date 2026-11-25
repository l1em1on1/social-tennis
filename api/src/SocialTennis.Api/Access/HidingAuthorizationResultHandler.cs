using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace SocialTennis.Api.Access;

/// <summary>
/// Answers 404 where a manager policy hid what its route names (ADR-0017), and
/// otherwise what the framework does: 401 or 403 (ASP.NET Core docs,
/// "Customize the behavior of AuthorizationMiddleware"). UseStatusCodePages
/// gives each its ProblemDetails body.
/// </summary>
public sealed class HidingAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler framework = new();

    public Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.OfType<HiddenFailureReason>().Any() == true)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        return framework.HandleAsync(next, context, policy, authorizeResult);
    }
}
