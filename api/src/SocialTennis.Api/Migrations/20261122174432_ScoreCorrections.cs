using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScoreCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Stages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Scores",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<Guid>(
                name: "ByPlayerId",
                table: "ScoreChanges",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ByUserId",
                table: "ScoreChanges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Every entry so far was a Player's: its User is the one holding them.
            migrationBuilder.Sql(
                """
                UPDATE "ScoreChanges" AS c SET "ByUserId" = p."UserId"
                FROM "Players" AS p
                WHERE p."Id" = c."ByPlayerId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ScoreChanges_ByUserId",
                table: "ScoreChanges",
                column: "ByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScoreChanges_Users_ByUserId",
                table: "ScoreChanges",
                column: "ByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScoreChanges_Users_ByUserId",
                table: "ScoreChanges");

            migrationBuilder.DropIndex(
                name: "IX_ScoreChanges_ByUserId",
                table: "ScoreChanges");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Scores");

            migrationBuilder.DropColumn(
                name: "ByUserId",
                table: "ScoreChanges");

            migrationBuilder.AlterColumn<Guid>(
                name: "ByPlayerId",
                table: "ScoreChanges",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
