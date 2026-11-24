using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SocialTennis.Api.Accounts;
using SocialTennis.Api.Authentication;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Features.Users;

/// <summary>
/// An Admin deletes a User (#116), for someone who asked to be forgotten
/// (ADR-0024): their Players become Former players, results intact, and the
/// User, their sessions and sign-in links go (<see cref="AccountDeletion"/>).
/// </summary>
/// <remarks>
/// 409 for an Admin's own account, an Admin by configuration, and the last
/// Admin: the app must never be left with nobody able to act (ADR-0022).
/// </remarks>
public static class DeleteUser
{
    public static async Task<Results<NoContent, NotFound, ProblemHttpResult>> HandleAsync(
        Guid userId,
        ClaimsPrincipal user,
        TennisDbContext db,
        IOptions<AuthOptions> authOptions,
        CancellationToken cancellationToken)
    {
        var outcome = await AccountDeletion.DeleteAsync(db, authOptions.Value, userId, user.UserId(), cancellationToken);
        return outcome switch
        {
            AccountDeletion.Outcome.Deleted => TypedResults.NoContent(),
            AccountDeletion.Outcome.NotFound => TypedResults.NotFound(),
            AccountDeletion.Outcome.OwnAccount => Refused(
                "own-account", "You can't delete your own account.", "Another Admin can."),
            AccountDeletion.Outcome.AdminBySetting => Refused(
                "admin-by-setting",
                "This User can't be deleted.",
                "Their email is an Admin in the app's configuration. Change it where the app is deployed."),
            _ => Refused("last-admin", "This User can't be deleted.", "They are the only Admin. Make someone else an Admin first."),
        };
    }

    private static ProblemHttpResult Refused(string type, string title, string detail) =>
        TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, type: type, title: title, detail: detail);
}
