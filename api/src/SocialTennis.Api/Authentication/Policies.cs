namespace SocialTennis.Api.Authentication;

/// <summary>
/// Named authorization policies, attached in each feature's routing table
/// (ADR-0017). Each manager policy is about the Club, League, Game or Player
/// its route names (<see cref="Access.ManagerAccess"/>); an Admin passes them all.
/// </summary>
public static class Policies
{
    public const string Admin = "Admin";

    /// <summary>A manager of anything: for a route that names nothing.</summary>
    public const string AnyManager = "AnyManager";

    /// <summary>Runs the Club in the route.</summary>
    public const string ClubManager = "ClubManager";

    /// <summary>Has any standing as a manager in the Club in the route.</summary>
    public const string ClubStaff = "ClubStaff";

    /// <summary>May create Leagues in the Club in the route.</summary>
    public const string LeagueCreator = "LeagueCreator";

    /// <summary>Runs the League in the route, or the League of the Game in it.</summary>
    public const string LeagueManager = "LeagueManager";

    /// <summary>Runs a Club the Player in the route is in.</summary>
    public const string PlayerManager = "PlayerManager";
}
