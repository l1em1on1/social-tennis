using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// Arranging a Game (#9): each Player's free days in a Stage, and a Game's
    /// agreed Date, its Court note and who set the Date.
    /// </summary>
    public partial class Availability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Court",
                table: "Games",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "Games",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DateSetById",
                table: "Games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AvailableDays",
                columns: table => new
                {
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvailableDays", x => new { x.StageId, x.PlayerId, x.Date });
                    table.ForeignKey(
                        name: "FK_AvailableDays_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AvailableDays_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_DateSetById",
                table: "Games",
                column: "DateSetById");

            migrationBuilder.CreateIndex(
                name: "IX_AvailableDays_PlayerId",
                table: "AvailableDays",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Players_DateSetById",
                table: "Games",
                column: "DateSetById",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Games_Players_DateSetById",
                table: "Games");

            migrationBuilder.DropTable(
                name: "AvailableDays");

            migrationBuilder.DropIndex(
                name: "IX_Games_DateSetById",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Court",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "DateSetById",
                table: "Games");
        }
    }
}
