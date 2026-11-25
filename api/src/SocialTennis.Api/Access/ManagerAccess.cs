using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Access;

/// <summary>
/// What the signed-in User may do as a manager, of which Club, League or
/// Player: the one place that decides it (ADR-0017, ADR-0034). The named
/// policies in the routing tables ask it (<see cref="ManagerAuthorizationHandler"/>),
/// and so do the handlers whose routes are for a League's Players as well as
/// its managers, which no policy alone can guard.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>An Admin</b> may do everything, everywhere. They are let through without a look: what isn't there is
/// the handler's 404.</item>
/// <item><b>A Club manager</b> runs their Club and every League in it.</item>
/// <item><b>A League manager</b> (#220) runs the Leagues they are assigned or created, and creates Leagues in a Club
/// whose role they hold. The two can be held apart: an assignment needs no role, and a role no assignment.</item>
/// <item><b>Anyone else</b> is refused if they can see what they asked about (a member of the Club, a participant
/// of the League, or with standing there), and otherwise it is hidden from them.</item>
/// </list>
/// Read on every call, so granting or removing a role takes effect at once.
/// </remarks>
public sealed class ManagerAccess(TennisDbContext db)
{
    /// <summary>Whether they manage anything at all, which is what shows them Manage.</summary>
    public async Task<bool> RunsAnythingAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (user.IsInRole(SessionRoles.Admin))
        {
            return true;
        }

        return await StandingIn(user.UserId()).AnyAsync(cancellationToken);
    }

    /// <summary>The Clubs they have any standing in, by name, and what they may do in each: every Club for an Admin.</summary>
    public async Task<IReadOnlyList<ClubStanding>> ClubsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var admin = user.IsInRole(SessionRoles.Admin);
        var userId = user.UserId();
        var standing = StandingIn(userId);
        var clubs = admin ? db.Clubs : db.Clubs.Where(c => standing.Contains(c.Id));
        return await clubs
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new ClubStanding(
                c.Id,
                c.Name,
                admin || db.ClubRoles.Any(r => r.ClubId == c.Id && r.UserId == userId && r.Kind == ClubRoleKind.Manager),
                admin || db.ClubRoles.Any(r => r.ClubId == c.Id && r.UserId == userId)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>A Club's Leagues they run: all of them for its managers and Admins, and their own for a League manager.</summary>
    public IQueryable<League> LeaguesRunIn(ClaimsPrincipal user, Guid clubId)
    {
        var inClub = db.Leagues.Where(l => l.ClubId == clubId);
        if (user.IsInRole(SessionRoles.Admin))
        {
            return inClub;
        }

        var userId = user.UserId();
        return inClub.Where(l =>
            db.ClubRoles.Any(r => r.ClubId == clubId && r.UserId == userId && r.Kind == ClubRoleKind.Manager)
            || db.LeagueManagers.Any(m => m.LeagueId == l.Id && m.UserId == userId));
    }

    public Task<ManagerAnswer> ClubManagerAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken)
    {
        var clubs = ClubOf(clubId);
        return AnswerAsync(user, clubs, userId => RunsClubAsync(userId, clubs, cancellationToken), null, cancellationToken);
    }

    public Task<ManagerAnswer> ClubStaffAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken)
    {
        var clubs = ClubOf(clubId);
        return AnswerAsync(
            user, clubs, userId => StandingIn(userId).AnyAsync(id => clubs.Contains(id), cancellationToken), null, cancellationToken);
    }

    public Task<ManagerAnswer> LeagueCreatorAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken)
    {
        var clubs = ClubOf(clubId);
        // Either kind of role creates Leagues: it's what a League manager's is for.
        return AnswerAsync(
            user, clubs, userId => db.ClubRoles.AnyAsync(r => r.UserId == userId && clubs.Contains(r.ClubId), cancellationToken), null, cancellationToken);
    }

    public Task<ManagerAnswer> LeagueManagerAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken) =>
        RunsLeagueAsync(user, db.Leagues.Where(l => l.Id == leagueId), cancellationToken);

    public Task<ManagerAnswer> LeagueManagerOfGameAsync(ClaimsPrincipal user, Guid gameId, CancellationToken cancellationToken) =>
        RunsLeagueAsync(user, db.Games.Where(g => g.Id == gameId).Select(g => g.Stage.League), cancellationToken);

    /// <summary>Of the Club the League is in: for what only its managers decide, who manages the League.</summary>
    public Task<ManagerAnswer> LeagueClubManagerAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken)
    {
        var league = db.Leagues.Where(l => l.Id == leagueId);
        var clubs = league.Select(l => l.ClubId);
        return AnswerAsync(user, clubs, userId => RunsClubAsync(userId, clubs, cancellationToken), Participants(league), cancellationToken);
    }

    /// <summary>Whether they run this League: for a handler whose route is for its Players too.</summary>
    public async Task<bool> RunsLeagueAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken) =>
        await LeagueManagerAsync(user, leagueId, cancellationToken) == ManagerAnswer.Allowed;

    /// <summary>Whether they run this Club: for a handler that shows its managers more.</summary>
    public async Task<bool> RunsClubAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        await ClubManagerAsync(user, clubId, cancellationToken) == ManagerAnswer.Allowed;

    /// <summary>Of any Club the Player is in, or has asked to join: its managers see them either way.</summary>
    public Task<ManagerAnswer> PlayerManagerAsync(ClaimsPrincipal user, Guid playerId, CancellationToken cancellationToken)
    {
        var clubs = db.ClubMemberships.Where(m => m.PlayerId == playerId).Select(m => m.ClubId);
        return AnswerAsync(
            user,
            clubs,
            userId => RunsClubAsync(userId, clubs, cancellationToken),
            userId => db.Players.Where(p => p.Id == playerId && p.UserId == userId).Select(p => p.Id),
            cancellationToken);
    }

    private Task<ManagerAnswer> RunsLeagueAsync(ClaimsPrincipal user, IQueryable<League> league, CancellationToken cancellationToken)
    {
        var clubs = league.Select(l => l.ClubId);
        return AnswerAsync(
            user,
            clubs,
            async userId => await RunsClubAsync(userId, clubs, cancellationToken)
                || await db.LeagueManagers.AnyAsync(m => m.UserId == userId && league.Any(l => l.Id == m.LeagueId), cancellationToken),
            Participants(league),
            cancellationToken);
    }

    private IQueryable<Guid> ClubOf(Guid clubId) => db.Clubs.Where(c => c.Id == clubId).Select(c => c.Id);

    /// <summary>The Clubs they have any standing in: a role there, or a League of its they run.</summary>
    private IQueryable<Guid> StandingIn(Guid userId) =>
        db.ClubRoles.Where(r => r.UserId == userId).Select(r => r.ClubId)
            .Concat(db.LeagueManagers.Where(m => m.UserId == userId).Select(m => m.League.ClubId));

    private Task<bool> RunsClubAsync(Guid userId, IQueryable<Guid> clubs, CancellationToken cancellationToken) =>
        db.ClubRoles.AnyAsync(
            r => r.UserId == userId && r.Kind == ClubRoleKind.Manager && clubs.Contains(r.ClubId), cancellationToken);

    /// <summary>The User's Players in the League, who can see it when they aren't in its Club.</summary>
    private Func<Guid, IQueryable<Guid>> Participants(IQueryable<League> league) =>
        userId => db.LeagueParticipants.Where(p => league.Any(l => l.Id == p.LeagueId) && p.Player.UserId == userId).Select(p => p.PlayerId);

    /// <param name="clubs">The Club the thing is in: none if it isn't there, or several for a Player.</param>
    /// <param name="allowed">Whether the User, by id, may do it.</param>
    /// <param name="theirs">The User's Players that let them see it when they aren't in its Club.</param>
    private async Task<ManagerAnswer> AnswerAsync(
        ClaimsPrincipal user,
        IQueryable<Guid> clubs,
        Func<Guid, Task<bool>> allowed,
        Func<Guid, IQueryable<Guid>>? theirs,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(SessionRoles.Admin))
        {
            return ManagerAnswer.Allowed;
        }

        var userId = user.UserId();
        if (await allowed(userId))
        {
            return ManagerAnswer.Allowed;
        }

        // A member of the Club, anyone with standing there, and whoever plays in the League.
        var sees = await db.ClubMemberships.AnyAsync(
                m => m.Player.UserId == userId && m.Status == ClubMembershipStatus.Active && clubs.Contains(m.ClubId), cancellationToken)
            || await StandingIn(userId).AnyAsync(id => clubs.Contains(id), cancellationToken);
        if (!sees && theirs is not null)
        {
            sees = await theirs(userId).AnyAsync(cancellationToken);
        }

        return sees ? ManagerAnswer.Refused : ManagerAnswer.Hidden;
    }
}
