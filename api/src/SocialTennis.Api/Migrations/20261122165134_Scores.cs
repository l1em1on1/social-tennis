using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class Scores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Clubs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Europe/London");

            migrationBuilder.CreateTable(
                name: "ScoreChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ByPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    After = table.Column<string>(type: "jsonb", nullable: false),
                    Before = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreChanges_Games_LeagueId_GameId",
                        columns: x => new { x.LeagueId, x.GameId },
                        principalTable: "Games",
                        principalColumns: new[] { "LeagueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScoreChanges_Players_ByPlayerId",
                        column: x => x.ByPlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Scores",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Winner = table.Column<int>(type: "integer", nullable: false),
                    Retired = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Sets = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_Scores_Games_LeagueId_GameId",
                        columns: x => new { x.LeagueId, x.GameId },
                        principalTable: "Games",
                        principalColumns: new[] { "LeagueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Scores_Players_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreChanges_ByPlayerId",
                table: "ScoreChanges",
                column: "ByPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreChanges_LeagueId_GameId",
                table: "ScoreChanges",
                columns: new[] { "LeagueId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_Scores_LeagueId_GameId",
                table: "Scores",
                columns: new[] { "LeagueId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scores_SubmittedById",
                table: "Scores",
                column: "SubmittedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreChanges");

            migrationBuilder.DropTable(
                name: "Scores");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Clubs");
        }
    }
}
