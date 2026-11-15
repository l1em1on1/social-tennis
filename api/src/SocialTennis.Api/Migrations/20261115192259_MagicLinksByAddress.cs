using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class MagicLinksByAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A link belongs to an address, not a User (#108). Links already
            // out keep working: each takes its User's email before the User
            // column goes. Their client is unknown (''), so they count towards
            // the address's total and towards no client's share; they expire
            // within the link lifetime anyway.
            migrationBuilder.AddColumn<string>(
                name: "ClientHash",
                table: "MagicLinkTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "MagicLinkTokens",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "MagicLinkTokens" t SET "Email" = u."Email"
                FROM "Users" u WHERE u."Id" = t."UserId";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MagicLinkTokens_Users_UserId",
                table: "MagicLinkTokens");

            migrationBuilder.DropIndex(
                name: "IX_MagicLinkTokens_UserId",
                table: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MagicLinkTokens");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_Email",
                table: "MagicLinkTokens",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A link for an address with no User has nowhere to go back to,
            // and links live minutes: going back drops them all.
            migrationBuilder.Sql("""DELETE FROM "MagicLinkTokens";""");

            migrationBuilder.DropIndex(
                name: "IX_MagicLinkTokens_Email",
                table: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "ClientHash",
                table: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "MagicLinkTokens");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "MagicLinkTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_UserId",
                table: "MagicLinkTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MagicLinkTokens_Users_UserId",
                table: "MagicLinkTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
