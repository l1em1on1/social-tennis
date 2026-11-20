using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class Teams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "LeagueParticipants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.UniqueConstraint("AK_Teams_LeagueId_Id", x => new { x.LeagueId, x.Id });
                    table.ForeignKey(
                        name: "FK_Teams_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueParticipants_LeagueId_TeamId",
                table: "LeagueParticipants",
                columns: new[] { "LeagueId", "TeamId" });

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueParticipants_Teams_LeagueId_TeamId",
                table: "LeagueParticipants",
                columns: new[] { "LeagueId", "TeamId" },
                principalTable: "Teams",
                principalColumns: new[] { "LeagueId", "Id" });

            // Every participant of a singles League (Types 1 to 3) is a Team of
            // one (#6). Doubles participants start unpaired, and Foursomes has
            // no Teams. The Team's id comes from the participant's key, so the
            // update finds the Team the insert made for it. Only participants
            // with no Team yet, so running it again changes nothing.
            migrationBuilder.Sql("""
                INSERT INTO "Teams" ("Id", "LeagueId", "CreatedAt")
                SELECT md5(p."LeagueId"::text || p."PlayerId"::text)::uuid, p."LeagueId", p."JoinedAt"
                FROM "LeagueParticipants" AS p
                JOIN "Leagues" AS l ON l."Id" = p."LeagueId"
                WHERE l."Type" IN (1, 2, 3) AND p."TeamId" IS NULL;

                UPDATE "LeagueParticipants" AS p
                SET "TeamId" = md5(p."LeagueId"::text || p."PlayerId"::text)::uuid
                FROM "Leagues" AS l
                WHERE l."Id" = p."LeagueId" AND l."Type" IN (1, 2, 3) AND p."TeamId" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueParticipants_Teams_LeagueId_TeamId",
                table: "LeagueParticipants");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_LeagueParticipants_LeagueId_TeamId",
                table: "LeagueParticipants");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "LeagueParticipants");
        }
    }
}
