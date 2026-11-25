using Microsoft.AspNetCore.Authorization;

namespace SocialTennis.Api.Access;

/// <summary>What a manager policy asks of its caller, about what its route names.</summary>
public enum ManagerStanding
{
    /// <summary>Manages something: the route names nothing.</summary>
    Any,

    /// <summary>Runs the route's Club.</summary>
    ClubManager,

    /// <summary>Has any standing in the route's Club: a role there, or a League of its they run.</summary>
    ClubStaff,

    /// <summary>May create Leagues in the route's Club.</summary>
    LeagueCreator,

    /// <summary>Runs the route's League, or the League of its Game.</summary>
    LeagueManager,

    /// <summary>Runs the Club of the route's League.</summary>
    LeagueClubManager,

    /// <summary>Runs a Club the route's Player is in.</summary>
    PlayerManager,
}

public sealed record ManagerRequirement(ManagerStanding Standing) : IAuthorizationRequirement;
