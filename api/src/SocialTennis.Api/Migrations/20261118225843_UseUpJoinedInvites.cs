using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// Invites are used up as they let someone in (#164). Deletes the ones that
    /// already have: before #164 an invite let in every Player its email's User
    /// held, when it was made or as they were added, so an invite whose User
    /// holds any Player has been used, even if that Player has left since.
    /// Invites waiting for a User's first Player stay.
    /// </summary>
    public partial class UseUpJoinedInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "ClubInvites" AS i
                USING "Users" AS u, "Players" AS p
                WHERE u."Email" = i."Email"
                  AND p."UserId" = u."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a used invite carried nothing a member doesn't.
        }
    }
}
