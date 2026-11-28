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
                g.StageId,
                g.Date,
                g.Court,
                DateSetBy = g.DateSetBy == null ? null : g.DateSetBy.Name,
                // Every Player it needs, on both Sides (#9): their free days make its shared ones.
                Needed = g.Sides.SelectMany(s => s.Team.Members).Select(m => new { m.PlayerId, m.Player.Name }).ToList(),
                // A Side is one Team, or in Foursomes two Players' Teams of one (ADR-0036), grouped below.
                Sides = g.Sides
                    .Select(s => new
                    {
                        s.Side,
                        s.TeamId,
                        Place = db.LadderPlaces.Where(p => p.StageId == g.StageId && p.TeamId == s.TeamId).Select(p => p.Position).FirstOrDefault(),
                        Withdrawn = s.Team.WithdrawnAt != null,
                        Players = s.Team.Members
                            .OrderBy(m => m.JoinedAt)
                            .ThenBy(m => m.PlayerId)
                            .Select(m => new GamePlayer(m.PlayerId, m.Player.Name, s.Team.WithdrawnAt != null))
                            .ToList(),
                    })
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

        var managers = await LeagueManagerList.NamesAsync(db, game.LeagueId, cancellationToken);
        var today = ClubCalendar.Today(game.TimeZone, clock);

        // The days that suit everyone (#9), for the Game's own Players while there's still a Stage to play it in.
        GameAvailability? availability = null;
        if (game.Yours.Count > 0 && !game.Stage.Closed)
        {
            var needed = game.Needed.Select(p => p.PlayerId).ToList();
            var free = await db.AvailableDays
                .Where(d => d.StageId == game.StageId && needed.Contains(d.PlayerId))
                .Select(d => new { d.PlayerId, d.Date })
                .ToListAsync(cancellationToken);
            availability = new GameAvailability(
                [.. free.Where(d => d.Date >= today).GroupBy(d => d.Date).Where(day => day.Count() == needed.Count).Select(day => day.Key).Order()],
                [.. game.Needed.Where(p => free.All(d => d.PlayerId != p.PlayerId)).Select(p => p.Name)]);
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
            [
                .. game.Sides
                    .GroupBy(s => s.Side)
                    .OrderBy(side => side.Key)
                    .Select(side => side.OrderBy(s => s.Place).ThenBy(s => s.TeamId).ToList())
                    .Select(teams => new GameSideSummary(
                        teams[0].Side,
                        teams[0].TeamId,
                        [.. teams.SelectMany(t => t.Players)],
                        teams.Count > 1 ? teams[1].TeamId : null,
                        teams.Any(t => t.Withdrawn))),
            ],
            score,
            game.NotPlayed,
            history,
            game.Yours,
            manages,
            managers,
            game.TimeZone,
            today,
            game.Date,
            game.Court,
            game.DateSetBy,
            availability));
    }
}
