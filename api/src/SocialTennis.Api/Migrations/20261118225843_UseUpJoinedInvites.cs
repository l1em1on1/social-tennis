using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// Invites are used up as they let someone in (#164). Deletes the ones that
    /// already have: the email's User holds a Player who is an active member
    /// (Status 1) of the invite's Club. Unused invites, and invites whose User's
    /// Players are only waiting or have left, stay.
    /// </summary>
    public partial class UseUpJoinedInvites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "ClubInvites" AS i
                USING "Users" AS u, "Players" AS p, "ClubMemberships" AS m
                WHERE u."Email" = i."Email"
                  AND p."UserId" = u."Id"
                  AND m."PlayerId" = p."Id"
                  AND m."ClubId" = i."ClubId"
                  AND m."Status" = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a used invite carried nothing a member doesn't.
        }
    }
}
