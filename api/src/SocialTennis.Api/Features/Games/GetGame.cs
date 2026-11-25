using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Access;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Contracts;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Games.Contracts;

namespace SocialTennis.Api.Features.Games;

/// <summary>
/// A League Game for its Match screen (#38): both Sides, its Score once
/// entered with every change to it, and which of the signed-in User's
/// Players play in it. Seen by
/// whoever can see its League's ladder (#7): the League's Players and Managers.
/// </summary>
/// <remarks>Anyone else gets a 404, as if the Game weren't there (ADR-0017).</remarks>
public static class GetGame
{
    public static async Task<Results<Ok<GetGameResponse>, NotFound>> HandleAsync(
        Guid gameId,
        ClaimsPrincipal user,
        TennisDbContext db,
        ManagerAccess access,
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
                Stage = new GameStage(g.Stage.Number, g.Stage.StartDate, g.Stage.EndDate, g.Stage.ClosedAt != null),
                g.Division,
                InLeague = g.Stage.League.Participants.Any(p => p.Player.UserId == userId),
                NotPlayed = g.VoidedAt != null,
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
                        SubmittedBy = g.Score.SubmittedBy != null
                            ? g.Score.SubmittedBy.Name
                            // A deleted Manager (#116) leaves no User: "A manager", never a Former player matched null to null.
                            : g.Score.SubmittedByUserId == null
                                ? null
                                : db.Players.Where(p => p.UserId == g.Score.SubmittedByUserId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.Name).FirstOrDefault(),
                        RecordedByManager = g.Score.SubmittedById == null,
                        g.Score.SubmittedAt,
                        g.Score.Version,
                    },
            })
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        var manages = game is not null && await access.RunsLeagueAsync(user, game.LeagueId, cancellationToken);
        if (game is null || !(game.InLeague || manages))
        {
            return TypedResults.NotFound();
        }

        var score = game.Score is { } s
            ? new GameScore(s.PlayedOn, SetScore.Of(game.ScoreFormat, s.Sets), s.Winner, s.Retired, s.SubmittedBy, s.RecordedByManager, s.SubmittedAt, s.Version)
            : null;
        var changes = await db.ScoreChanges
            .Where(c => c.LeagueId == game.LeagueId && c.GameId == gameId)
            .OrderByDescending(c => c.At)
            .ThenByDescending(c => c.Kind)
            .Select(c => new
            {
                c.Kind,
                By = c.ByPlayer != null
                    ? c.ByPlayer.Name
                    : c.ByUserId == null
                        ? null
                        : db.Players.Where(p => p.UserId == c.ByUserId).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.Name).FirstOrDefault(),
                c.At,
                c.Before,
                c.After,
            })
            .ToListAsync(cancellationToken);
        var snapshot = (ScoreValue value) => new ScoreSnapshot(value.PlayedOn, SetScore.Of(game.ScoreFormat, value.Sets), value.Winner, value.Retired);
        var history = changes
            .Select(c => new ScoreHistoryEntry(c.Kind, c.By, c.At, c.Before is { } before ? snapshot(before) : null, snapshot(c.After)))
            .ToList();
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
            game.NotPlayed,
            history,
            game.Yours,
            manages,
            game.TimeZone,
            ClubCalendar.Today(game.TimeZone, clock)));
    }
}
