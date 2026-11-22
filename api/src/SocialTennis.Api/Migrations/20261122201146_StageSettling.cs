using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class StageSettling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedById",
                table: "Scores",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "Scores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Every Score so far was a Player's: its User is the one holding them.
            migrationBuilder.Sql(
                """
                UPDATE "Scores" AS s SET "SubmittedByUserId" = p."UserId"
                FROM "Players" AS p
                WHERE p."Id" = s."SubmittedById";
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "Games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "Games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scores_SubmittedByUserId",
                table: "Scores",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_VoidedByUserId",
                table: "Games",
                column: "VoidedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Games_Users_VoidedByUserId",
                table: "Games",
                column: "VoidedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Scores_Users_SubmittedByUserId",
                table: "Scores",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy: before this migration a Score was always a Player's, so the
            // Scores a Manager recorded, which name no Player, can't be kept. They
            // go with their history; their Games are unscored again.
            migrationBuilder.Sql(
                """
                DELETE FROM "ScoreChanges" AS c USING "Scores" AS s
                WHERE s."GameId" = c."GameId" AND s."SubmittedById" IS NULL;
                DELETE FROM "Scores" WHERE "SubmittedById" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Games_Users_VoidedByUserId",
                table: "Games");

            migrationBuilder.DropForeignKey(
                name: "FK_Scores_Users_SubmittedByUserId",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Scores_SubmittedByUserId",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Games_VoidedByUserId",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "Scores");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "Games");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmittedById",
                table: "Scores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
