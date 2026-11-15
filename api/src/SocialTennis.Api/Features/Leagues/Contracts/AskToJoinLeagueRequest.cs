namespace SocialTennis.Api.Features.Leagues.Contracts;

/// <param name="PlayerId">Which of the signed-in User's Players is asking.</param>
public record AskToJoinLeagueRequest(Guid PlayerId);
