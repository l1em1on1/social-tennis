using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class JoinAsTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartnerId",
                table: "LeagueJoinRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJoinRequests_PartnerId",
                table: "LeagueJoinRequests",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueJoinRequests_Players_PartnerId",
                table: "LeagueJoinRequests",
                column: "PartnerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueJoinRequests_Players_PartnerId",
                table: "LeagueJoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeagueJoinRequests_PartnerId",
                table: "LeagueJoinRequests");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "LeagueJoinRequests");
        }
    }
}
