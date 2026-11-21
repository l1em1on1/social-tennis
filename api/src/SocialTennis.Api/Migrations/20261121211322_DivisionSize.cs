using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class DivisionSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DivisionSize",
                table: "Leagues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Division",
                table: "LadderPlaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Divisional Leagues of Teams, made before Divisions had a size,
            // get the default, 4 (ADR-0031). Knockout and Foursomes keep none.
            migrationBuilder.Sql("""
                UPDATE "Leagues" SET "DivisionSize" = 4
                WHERE "Structure" = 1 AND "Type" <> 7 AND "DivisionSize" IS NULL;
                """);

            // Places from a start made before Divisions were stored had
            // Divisions of 2 read from their positions, and an odd last place
            // had the Bye. Each gets that Division, and the old Bye joins the
            // Division above it. Its Games stay as they were: production had no
            // started League before this migration.
            migrationBuilder.Sql("""
                UPDATE "LadderPlaces" AS p
                SET "Division" = LEAST((p."Position" + 1) / 2, GREATEST(1, c.n / 2))
                FROM (SELECT "StageId", COUNT(*) AS n FROM "LadderPlaces" GROUP BY "StageId") AS c
                WHERE c."StageId" = p."StageId" AND p."Division" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DivisionSize",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Division",
                table: "LadderPlaces");
        }
    }
}
