using Microsoft.AspNetCore.Authorization;
using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.Access;

public static class AccessServiceCollectionExtensions
{
    /// <summary>The manager policies' names, and what each asks.</summary>
    public static readonly IReadOnlyDictionary<string, ManagerStanding> ManagerPolicies = new Dictionary<string, ManagerStanding>
    {
        [Policies.AnyManager] = ManagerStanding.Any,
        [Policies.ClubManager] = ManagerStanding.ClubManager,
        [Policies.ClubStaff] = ManagerStanding.ClubStaff,
        [Policies.LeagueCreator] = ManagerStanding.LeagueCreator,
        [Policies.LeagueManager] = ManagerStanding.LeagueManager,
        [Policies.LeagueClubManager] = ManagerStanding.LeagueClubManager,
        [Policies.PlayerManager] = ManagerStanding.PlayerManager,
    };

    /// <summary>Whether this policy can answer 404, hiding what its route names.</summary>
    public static bool Hides(string? policy) =>
        policy is not null && ManagerPolicies.TryGetValue(policy, out var standing) && standing != ManagerStanding.Any;

    /// <summary>
    /// Deny by default (ADR-0017): an endpoint that declares nothing requires a
    /// signed-in User. Anonymous routes opt out with AllowAnonymous(), so a
    /// forgotten declaration fails closed (ASP.NET Core docs, "fallback policy").
    /// The named policies are each about what their route names.
    /// </summary>
    public static IServiceCollection AddAccess(this IServiceCollection services)
    {
        // Scoped: the rule reads the database.
        services.AddScoped<ManagerAccess>();
        services.AddScoped<IAuthorizationHandler, ManagerAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, HidingAuthorizationResultHandler>();

        var authorization = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(SessionRoles.Admin));
        foreach (var (name, standing) in ManagerPolicies)
        {
            authorization.AddPolicy(name, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new ManagerRequirement(standing)));
        }

        return services;
    }
}
