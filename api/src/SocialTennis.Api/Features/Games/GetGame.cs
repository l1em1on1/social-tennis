using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A League Game for its Match screen (#38): both Sides, its Score once
/// entered, and which of the signed-in User's Players play in it. Seen by
/// whoever can see its League's ladder (#7): the League's Players and Managers.
/// </summary>
/// <remarks>Anyone else gets a 404, as if the Game weren't there (ADR-0017).</remarks>
public static class GetGame
{
    public static async Task<Results<Ok<GetGameResponse>, NotFound>> HandleAsync(
        Guid gameId,
        ClaimsPrincipal user,
        TennisDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = user.UserId();
        var game = await db.Games
            .Where(g => g.Id == gameId)
            .Select(g => new
            {
                g.Id,
                g.LeagueId,
                LeagueName = g.Stage.League.Name,
                ClubName = g.Stage.League.Club.Name,
                g.Stage.League.Club.TimeZone,
                g.Stage.League.Type,
                g.Stage.League.ScoreFormat,
                Stage = new GameStage(g.Stage.Number, g.Stage.StartDate, g.Stage.EndDate),
                g.Division,
                InLeague = g.Stage.League.Participants.Any(p => p.Player.UserId == userId),
                Sides = g.Sides
                    .OrderBy(s => s.Side)
                    .Select(s => new GameSideSummary(
                        s.Side,
                        s.TeamId,
                        s.Team.Members
                            .OrderBy(m => m.JoinedAt)
                            .ThenBy(m => m.PlayerId)
                            .Select(m => new GamePlayer(m.PlayerId, m.Player.Name))
                            .ToList()))
                    .ToList(),
                Yours = g.Sides
                    .SelectMany(s => s.Team.Members)
                    .Where(m => m.Player.UserId == userId)
                    .Select(m => m.PlayerId)
                    .ToList(),
                Score = g.Score == null
                    ? null
                    : new
                    {
                        g.Score.PlayedOn,
                        g.Score.Sets,
                        g.Score.Winner,
                        g.Score.Retired,
                        SubmittedBy = g.Score.SubmittedBy.Name,
                        g.Score.SubmittedAt,
                    },
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (game is null || !(game.InLeague || user.IsInRole(Policies.Manager)))
        {
            return TypedResults.NotFound();
        }

        var score = game.Score is { } s
            ? new GameScore(s.PlayedOn, SetScore.Of(game.ScoreFormat, s.Sets), s.Winner, s.Retired, s.SubmittedBy, s.SubmittedAt)
            : null;
        return TypedResults.Ok(new GetGameResponse(
            game.Id,
            game.LeagueId,
            game.LeagueName,
            game.ClubName,
            game.Type,
            game.ScoreFormat,
            game.Stage,
            game.Division,
            game.Sides,
            score,
            game.Yours,
            ClubCalendar.Today(game.TimeZone, clock)));
    }
}
