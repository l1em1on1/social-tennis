using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// How a League's Standings rank (#214, ADR-0035): Standard (1) or Bonus
    /// points (2), for a Divisional League of Teams. Existing ones are Standard.
    /// </summary>
    public partial class StandingsMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StandingsMode",
                table: "Leagues",
                type: "integer",
                nullable: true);

            // Every Divisional League of Teams ranked as Standard ranks now (#214); Knockout and Foursomes have no mode.
            migrationBuilder.Sql("""
                UPDATE "Leagues" SET "StandingsMode" = 1
                WHERE "Structure" = 1 AND "Type" <> 7;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandingsMode",
                table: "Leagues");
        }
    }
}
