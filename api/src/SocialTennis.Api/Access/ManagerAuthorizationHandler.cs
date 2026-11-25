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
        var allowed = requirement.Standing switch
        {
            ManagerStanding.Any => await access.RunsAnythingAsync(user, cancelled),
            ManagerStanding.ClubManager => await access.RunsClubAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.ClubStaff => await access.HasStandingInClubAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.LeagueCreator => await access.CreatesLeaguesInClubAsync(user, Id(http, "clubId"), cancelled),
            ManagerStanding.LeagueManager => http.Request.RouteValues.ContainsKey("leagueId")
                ? await access.RunsLeagueAsync(user, Id(http, "leagueId"), cancelled)
                : await access.RunsLeagueOfGameAsync(user, Id(http, "gameId"), cancelled),
            ManagerStanding.PlayerManager => await access.RunsAClubOfPlayerAsync(user, Id(http, "playerId"), cancelled),
            _ => throw new InvalidOperationException($"No rule for {requirement.Standing}."),
        };
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }

    /// <summary>An id from the route. One that isn't there is a policy on the wrong route: a fault, not a refusal.</summary>
    private static Guid Id(HttpContext http, string name) =>
        Guid.TryParse(http.Request.RouteValues[name]?.ToString(), out var id)
            ? id
            : throw new InvalidOperationException($"The route of {http.GetEndpoint()?.DisplayName} has no {name} for its manager policy.");
}
