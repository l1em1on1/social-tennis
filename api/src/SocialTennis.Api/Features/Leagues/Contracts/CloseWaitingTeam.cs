namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <summary>A Team in the League but off the ladder, which the Manager can add at close (#10).</summary>
public record CloseWaitingTeam(Guid TeamId, IReadOnlyList<LadderPlayer> Players);
