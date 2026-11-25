namespace SocialTennis.Api.Access;

/// <summary>What a User may do in one Club: what <c>GET /clubs</c> says of each.</summary>
/// <param name="CanManage">Runs the Club: its members, its Leagues and who manages those.</param>
/// <param name="CanCreateLeagues">May create Leagues there.</param>
public sealed record ClubStanding(Guid ClubId, string Name, bool CanManage, bool CanCreateLeagues);
