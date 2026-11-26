using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// Foursomes (#14, ADR-0036): a Game's Side may hold two Players' Teams of
    /// one, so a Side row is keyed by its Team, not its Side number; and every
    /// Foursomes participant, who had no Team, gets a Team of one.
    /// </summary>
    public partial class FoursomesSides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GameSides",
                table: "GameSides");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameSides",
                table: "GameSides",
                columns: new[] { "GameId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSides_GameId_Side",
                table: "GameSides",
                columns: new[] { "GameId", "Side" });

            // A Team of one for each Foursomes (Type 7) participant without one, made when they joined.
            // The CTE calls a volatile function, so Postgres evaluates it once for both statements.
            migrationBuilder.Sql("""
                WITH missing AS (
                    SELECT p."LeagueId", p."PlayerId", p."JoinedAt", gen_random_uuid() AS "TeamId"
                    FROM "LeagueParticipants" p
                    JOIN "Leagues" l ON l."Id" = p."LeagueId"
                    WHERE l."Type" = 7 AND p."TeamId" IS NULL
                ), teams AS (
                    INSERT INTO "Teams" ("Id", "LeagueId", "CreatedAt")
                    SELECT "TeamId", "LeagueId", "JoinedAt" FROM missing
                )
                UPDATE "LeagueParticipants" p SET "TeamId" = m."TeamId"
                FROM missing m
                WHERE p."LeagueId" = m."LeagueId" AND p."PlayerId" = m."PlayerId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GameSides",
                table: "GameSides");

            migrationBuilder.DropIndex(
                name: "IX_GameSides_GameId_Side",
                table: "GameSides");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameSides",
                table: "GameSides",
                columns: new[] { "GameId", "Side" });
        }
    }
}
