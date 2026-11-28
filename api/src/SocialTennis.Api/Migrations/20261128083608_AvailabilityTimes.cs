using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class AvailabilityTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "Games",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "From",
                table: "AvailableDays",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotFree",
                table: "AvailableDays",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "To",
                table: "AvailableDays",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UsualDays",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weekday = table.Column<int>(type: "integer", nullable: false),
                    From = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    To = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsualDays", x => new { x.PlayerId, x.Weekday });
                    table.CheckConstraint("CK_UsualDays_Window", "(\"From\" IS NULL AND \"To\" IS NULL) OR (\"From\" IS NOT NULL AND \"To\" IS NOT NULL AND \"From\" < \"To\")");
                    table.ForeignKey(
                        name: "FK_UsualDays_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AvailableDays_NotFree",
                table: "AvailableDays",
                sql: "NOT \"NotFree\" OR \"From\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AvailableDays_Window",
                table: "AvailableDays",
                sql: "(\"From\" IS NULL AND \"To\" IS NULL) OR (\"From\" IS NOT NULL AND \"To\" IS NOT NULL AND \"From\" < \"To\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsualDays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AvailableDays_NotFree",
                table: "AvailableDays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AvailableDays_Window",
                table: "AvailableDays");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "From",
                table: "AvailableDays");

            migrationBuilder.DropColumn(
                name: "NotFree",
                table: "AvailableDays");

            migrationBuilder.DropColumn(
                name: "To",
                table: "AvailableDays");
        }
    }
}
