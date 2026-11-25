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
/// <item><b>Anyone else</b> is refused if they can see what they asked about (a member of the Club, a participant
/// of the League, or with a role there), and otherwise it is hidden from them.</item>
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

        var userId = user.UserId();
        return await db.ClubRoles.AnyAsync(r => r.UserId == userId, cancellationToken);
    }

    /// <summary>The Clubs they have a role in: every Club for an Admin.</summary>
    public IQueryable<Club> ClubsOf(ClaimsPrincipal user)
    {
        if (user.IsInRole(SessionRoles.Admin))
        {
            return db.Clubs;
        }

        var userId = user.UserId();
        return db.Clubs.Where(c => db.ClubRoles.Any(r => r.ClubId == c.Id && r.UserId == userId));
    }

    public Task<ManagerAnswer> ClubManagerAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        AnswerAsync(user, db.Clubs.Where(c => c.Id == clubId).Select(c => c.Id), null, cancellationToken);

    public Task<ManagerAnswer> ClubStaffAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        ClubManagerAsync(user, clubId, cancellationToken);

    public Task<ManagerAnswer> LeagueCreatorAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        ClubManagerAsync(user, clubId, cancellationToken);

    public Task<ManagerAnswer> LeagueManagerAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken) =>
        AnswerAsync(
            user,
            db.Leagues.Where(l => l.Id == leagueId).Select(l => l.ClubId),
            userId => db.LeagueParticipants.Where(p => p.LeagueId == leagueId && p.Player.UserId == userId).Select(p => p.PlayerId),
            cancellationToken);

    public Task<ManagerAnswer> LeagueManagerOfGameAsync(ClaimsPrincipal user, Guid gameId, CancellationToken cancellationToken)
    {
        var game = db.Games.Where(g => g.Id == gameId);
        return AnswerAsync(
            user,
            game.Select(g => g.Stage.League.ClubId),
            userId => db.LeagueParticipants.Where(p => game.Any(g => g.LeagueId == p.LeagueId) && p.Player.UserId == userId).Select(p => p.PlayerId),
            cancellationToken);
    }

    /// <summary>Whether they run this League: for a handler whose route is for its Players too.</summary>
    public async Task<bool> RunsLeagueAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken) =>
        await LeagueManagerAsync(user, leagueId, cancellationToken) == ManagerAnswer.Allowed;

    /// <summary>Of any Club the Player is in, or has asked to join: its managers see them either way.</summary>
    public Task<ManagerAnswer> PlayerManagerAsync(ClaimsPrincipal user, Guid playerId, CancellationToken cancellationToken) =>
        AnswerAsync(
            user,
            db.ClubMemberships.Where(m => m.PlayerId == playerId).Select(m => m.ClubId),
            userId => db.Players.Where(p => p.Id == playerId && p.UserId == userId).Select(p => p.Id),
            cancellationToken);

    /// <param name="clubs">The Club the thing is in: none if it isn't there, or several for a Player.</param>
    /// <param name="theirs">The User's Players that let them see it when they aren't in its Club.</param>
    private async Task<ManagerAnswer> AnswerAsync(
        ClaimsPrincipal user,
        IQueryable<Guid> clubs,
        Func<Guid, IQueryable<Guid>>? theirs,
        CancellationToken cancellationToken)
    {
        if (user.IsInRole(SessionRoles.Admin))
        {
            return ManagerAnswer.Allowed;
        }

        var userId = user.UserId();
        if (await db.ClubRoles.AnyAsync(
            r => r.UserId == userId && r.Kind == ClubRoleKind.Manager && clubs.Contains(r.ClubId), cancellationToken))
        {
            return ManagerAnswer.Allowed;
        }

        var sees = await db.ClubMemberships.AnyAsync(
            m => m.Player.UserId == userId && m.Status == ClubMembershipStatus.Active && clubs.Contains(m.ClubId), cancellationToken);
        if (!sees && theirs is not null)
        {
            sees = await theirs(userId).AnyAsync(cancellationToken);
        }

        return sees ? ManagerAnswer.Refused : ManagerAnswer.Hidden;
    }
}
