using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class LeagueStart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stages", x => x.Id);
                    table.UniqueConstraint("AK_Stages_LeagueId_Id", x => new { x.LeagueId, x.Id });
                    table.ForeignKey(
                        name: "FK_Stages_Leagues_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "Leagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Division = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.UniqueConstraint("AK_Games_LeagueId_Id", x => new { x.LeagueId, x.Id });
                    table.ForeignKey(
                        name: "FK_Games_Stages_LeagueId_StageId",
                        columns: x => new { x.LeagueId, x.StageId },
                        principalTable: "Stages",
                        principalColumns: new[] { "LeagueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LadderPlaces",
                columns: table => new
                {
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LadderPlaces", x => new { x.StageId, x.Position });
                    table.ForeignKey(
                        name: "FK_LadderPlaces_Stages_LeagueId_StageId",
                        columns: x => new { x.LeagueId, x.StageId },
                        principalTable: "Stages",
                        principalColumns: new[] { "LeagueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LadderPlaces_Teams_LeagueId_TeamId",
                        columns: x => new { x.LeagueId, x.TeamId },
                        principalTable: "Teams",
                        principalColumns: new[] { "LeagueId", "Id" });
                });

            migrationBuilder.CreateTable(
                name: "GameSides",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSides", x => new { x.GameId, x.Side });
                    table.ForeignKey(
                        name: "FK_GameSides_Games_LeagueId_GameId",
                        columns: x => new { x.LeagueId, x.GameId },
                        principalTable: "Games",
                        principalColumns: new[] { "LeagueId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameSides_Teams_LeagueId_TeamId",
                        columns: x => new { x.LeagueId, x.TeamId },
                        principalTable: "Teams",
                        principalColumns: new[] { "LeagueId", "Id" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_LeagueId_StageId",
                table: "Games",
                columns: new[] { "LeagueId", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_StageId_Division",
                table: "Games",
                columns: new[] { "StageId", "Division" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSides_LeagueId_GameId",
                table: "GameSides",
                columns: new[] { "LeagueId", "GameId" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSides_LeagueId_TeamId",
                table: "GameSides",
                columns: new[] { "LeagueId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderPlaces_LeagueId_StageId",
                table: "LadderPlaces",
                columns: new[] { "LeagueId", "StageId" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderPlaces_LeagueId_TeamId",
                table: "LadderPlaces",
                columns: new[] { "LeagueId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_LadderPlaces_StageId_TeamId",
                table: "LadderPlaces",
                columns: new[] { "StageId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_LeagueId_Number",
                table: "Stages",
                columns: new[] { "LeagueId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSides");

            migrationBuilder.DropTable(
                name: "LadderPlaces");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "Stages");
        }
    }
}
