using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Leagues;

/// <summary>
/// The one place participants come and go with their Teams (#6). Every caller
/// holds the League's lock (<see cref="LeagueEntry.LockLeagueAsync"/>), so these
/// hold together: a singles participant has a Team of one; a doubles Team has
/// two Players, and a doubles participant is in one or is unpaired; a
/// <see cref="LeagueType.Foursomes"/> participant has none.
/// </summary>
internal static class LeagueTeams
{
    /// <summary>
    /// Adds a participant, with a Team of one in a singles League. In a doubles
    /// League they start unpaired, for the Manager to pair.
    /// </summary>
    public static void AddParticipant(
        TennisDbContext db, League league, Guid playerId, DateTimeOffset joinedAt, DateTimeOffset now)
    {
        var participant = new LeagueParticipant { LeagueId = league.Id, PlayerId = playerId, JoinedAt = joinedAt };
        if (league.Type.TeamSize() == 1)
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
}
