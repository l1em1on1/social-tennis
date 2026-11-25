using Microsoft.AspNetCore.Authorization;

namespace SocialTennis.Api.Access;

/// <summary>
/// Answers a <see cref="ManagerRequirement"/> from the ids in the request's
/// route. With endpoint routing the resource is the HttpContext (ASP.NET Core
/// docs, "Access MVC request context in handlers").
/// </summary>
public sealed class ManagerAuthorizationHandler(ManagerAccess access) : AuthorizationHandler<ManagerRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManagerRequirement requirement)
    {
        // Every handler runs, even once the policy's signed-in requirement has failed.
        if (context.User.Identity is not { IsAuthenticated: true } || context.Resource is not HttpContext http)
        {
            return;
        }

        var user = context.User;
        var cancelled = http.RequestAborted;
        var answer = requirement.Standing switch
        {
            ManagerStanding.Any => await access.RunsAnythingAsync(user, cancelled) ? ManagerAnswer.Allowed : ManagerAnswer.Refused,
            ManagerStanding.ClubManager => await access.ClubManagerAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.ClubStaff => await access.ClubStaffAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.LeagueCreator => await access.LeagueCreatorAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.LeagueManager => http.Request.RouteValues.ContainsKey("leagueId")
                ? await access.LeagueManagerAsync(user, Id(http, "leagueId"), cancelled)
                : await access.LeagueManagerOfGameAsync(user, Id(http, "gameId"), cancelled),
            ManagerStanding.PlayerManager => await access.PlayerManagerAsync(user, Id(http, "playerId"), cancelled),
            _ => throw new InvalidOperationException($"No rule for {requirement.Standing}."),
        };
        // Refused leaves the requirement unmet, which is a 403.
        if (answer == ManagerAnswer.Allowed)
        {
            context.Succeed(requirement);
        }
        else if (answer == ManagerAnswer.Hidden)
        {
            context.Fail(new HiddenFailureReason(this));
        }
    }

    /// <summary>An id from the route. One that isn't there is a policy on the wrong route: a fault, not a refusal.</summary>
    private static Guid Id(HttpContext http, string name) =>
        Guid.TryParse(http.Request.RouteValues[name]?.ToString(), out var id)
            ? id
            : throw new InvalidOperationException($"The route of {http.GetEndpoint()?.DisplayName} has no {name} for its manager policy.");
}
