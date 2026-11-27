using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <summary>
    /// The Players' answers to Foursomes ties (#14 part 3, ADR-0036): who goes
    /// up, or down, one per place a Division's tie decides.
    /// </summary>
    public partial class TieMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TieMarks",
                columns: table => new
                {
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Division = table.Column<int>(type: "integer", nullable: false),
                    Place = table.Column<int>(type: "integer", nullable: false),
                    LeagueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Group = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkedById = table.Column<Guid>(type: "uuid", nullable: true),
                    MarkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TieMarks", x => new { x.StageId, x.Division, x.Place });
                    table.ForeignKey(
                        name: "FK_TieMarks_Players_MarkedById",
                        column: x => x.MarkedById,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TieMarks_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TieMarks_MarkedById",
                table: "TieMarks",
                column: "MarkedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TieMarks");
        }
    }
}
