using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The one place participants come and go with their Teams (#6). Every caller
/// holds the League's lock (<see cref="LeagueEntry.LockLeagueAsync"/>), so these
/// hold together: a singles participant has a Team of one; a doubles Team has
/// two Players, and a doubles participant is in one or is unpaired; a
/// <see cref="LeagueType.Foursomes"/> participant has a Team of one too, never
/// a partner, since partners change every Game (ADR-0036).
/// </summary>
internal static class LeagueTeams
{
    /// <summary>
    /// Adds a participant, with a Team of one in a singles or a Foursomes League
    /// (ADR-0036: in Foursomes it holds the Player's place and Sides). In a doubles
    /// League they start unpaired, for the Manager to pair; a pair joining
    /// together comes in through <see cref="EnterAsPairAsync"/>.
    /// </summary>
    public static void AddParticipant(
        TennisDbContext db, League league, Guid playerId, DateTimeOffset joinedAt, DateTimeOffset now)
    {
        var participant = new LeagueParticipant { LeagueId = league.Id, PlayerId = playerId, JoinedAt = joinedAt };
        if (league.Type.HasTeamsOfOne())
        {
            var team = new Team { Id = Guid.NewGuid(), LeagueId = league.Id, CreatedAt = now };
            db.Teams.Add(team);
            participant.TeamId = team.Id;
        }
        db.LeagueParticipants.Add(participant);
    }

    /// <summary>
    /// Takes a participant out with their Team: a Team of one goes, and a
    /// doubles partner goes back to the unpaired Players. False if they
    /// weren't in the League.
    /// </summary>
    public static async Task<bool> RemoveParticipantAsync(
        TennisDbContext db, Guid leagueId, Guid playerId, CancellationToken cancellationToken)
    {
        var participant = await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == playerId)
            .Select(p => new { p.TeamId })
            .SingleOrDefaultAsync(cancellationToken);
        if (participant is null)
        {
            return false;
        }
        if (participant.TeamId is { } teamId)
        {
            await BreakUpAsync(db, leagueId, teamId, cancellationToken);
        }
        await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Deletes a Team, leaving its members in the League unpaired. Their TeamId
    /// is cleared first: the reference has no cascade, because a cascade would
    /// also clear the League's id, which is part of their key.
    /// </summary>
    public static async Task<bool> BreakUpAsync(
        TennisDbContext db, Guid leagueId, Guid teamId, CancellationToken cancellationToken)
    {
        await db.LeagueParticipants
            .Where(p => p.LeagueId == leagueId && p.TeamId == teamId)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.TeamId, (Guid?)null), cancellationToken);
        return await db.Teams
            .Where(t => t.LeagueId == leagueId && t.Id == teamId)
            .ExecuteDeleteAsync(cancellationToken) > 0;
    }

    /// <summary>
    /// Puts two Players in a doubles League as a Team, from a request naming a
    /// partner. One already in the League without a partner (the Manager added
    /// them) keeps their join time; the other joins at <paramref name="joinedAt"/>.
    /// Null when done; a 409 if either already has a partner, with nothing changed.
    /// </summary>
    public static async Task<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult?> EnterAsPairAsync(
        TennisDbContext db,
        League league,
        Guid playerId,
        Guid partnerId,
        DateTimeOffset joinedAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var playerIds = new[] { playerId, partnerId };
        var already = await db.LeagueParticipants
            .Where(p => p.LeagueId == league.Id && playerIds.Contains(p.PlayerId))
            .ToListAsync(cancellationToken);
        if (already.Any(p => p.TeamId is not null))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: "already-in-a-team",
                title: "One of the pair already has a partner in this League.",
                detail: "Unpair them first. Or the Player who asked can take the request back and ask with someone else; rejecting it stops them asking again, though a Manager can still add them.");
        }

        var team = new Team { Id = Guid.NewGuid(), LeagueId = league.Id, CreatedAt = now };
        db.Teams.Add(team);
        foreach (var id in playerIds)
        {
            if (already.SingleOrDefault(p => p.PlayerId == id) is { } participant)
            {
                participant.TeamId = team.Id;
            }
            else
            {
                db.LeagueParticipants.Add(new LeagueParticipant
                {
                    LeagueId = league.Id,
                    PlayerId = id,
                    JoinedAt = joinedAt,
                    TeamId = team.Id,
                });
            }
        }
        return null;
    }

    /// <summary>The 409 for pairing, unpairing or naming a partner in a League that isn't doubles.</summary>
    public static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult NotDoubles() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "not-a-doubles-league",
            title: "Only a doubles League's Players have partners.",
            detail: "In a singles League each Player is a Team of one. In Foursomes partners change every Game, so nobody is paired.");
}
