using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// Invites are used up as they let someone in (#164). Deletes the ones that
    /// have: the email's User holds a Player in the invite's Club. Not merely
    /// any Player: a Manager adding one in another Club never used this
    /// invite. An invite whose Player has since left the Club stays, as
    /// nothing records the leaving; it is used up when they ask again, or when
    /// the User adds a Player.
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
                  AND m."ClubId" = i."ClubId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to restore: a used invite carried nothing a member doesn't.
        }
    }
}
