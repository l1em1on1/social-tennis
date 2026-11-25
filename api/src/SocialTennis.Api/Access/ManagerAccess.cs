using System.Security.Claims;
using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.Access;

/// <summary>
/// What the signed-in User may do as a manager, of which Club, League or
/// Player: the one place that decides it (ADR-0017). The named policies in the
/// routing tables ask it (<see cref="ManagerAuthorizationHandler"/>), and so do
/// the handlers whose routes are for a League's Players as well as its
/// managers, which no policy alone can guard.
/// </summary>
/// <remarks>
/// Manager is one role for every Club until #110 gives each Club its own: until
/// then the id asked about makes no difference.
/// </remarks>
public sealed class ManagerAccess
{
    public Task<bool> RunsAnythingAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> RunsClubAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> HasStandingInClubAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> CreatesLeaguesInClubAsync(ClaimsPrincipal user, Guid clubId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> RunsLeagueAsync(ClaimsPrincipal user, Guid leagueId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> RunsLeagueOfGameAsync(ClaimsPrincipal user, Guid gameId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));

    public Task<bool> RunsAClubOfPlayerAsync(ClaimsPrincipal user, Guid playerId, CancellationToken cancellationToken) =>
        Task.FromResult(user.IsInRole(SessionRoles.Manager));
}
