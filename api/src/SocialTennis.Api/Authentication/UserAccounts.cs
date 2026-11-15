using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;

namespace SocialTennis.Api.Authentication;

public static class UserAccounts
{
    /// <summary>
    /// The id of the User for this email, creating the row if there is none.
    /// Safe inside a transaction and against a concurrent first sign-in: the
    /// insert yields to an existing row instead of failing.
    /// </summary>
    /// <remarks>
    /// A User row is not proof anyone has signed in. It exists for any address
    /// a sign-in link was asked for, or that a Manager added a Player for; only
    /// someone who can read that address's mail ever acts as it.
    /// </remarks>
    public static async Task<Guid> EnsureAsync(
        TennisDbContext db, string normalizedEmail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "Users" ("Id", "Email", "CreatedAt", "Roles")
            VALUES ({id}, {normalizedEmail}, {now}, 0)
            ON CONFLICT ("Email") DO NOTHING
            """,
            cancellationToken);
        return await db.Users.Where(u => u.Email == normalizedEmail).Select(u => u.Id).SingleAsync(cancellationToken);
    }
}
