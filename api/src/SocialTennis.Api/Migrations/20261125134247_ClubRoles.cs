using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClubRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClubRoles",
                columns: table => new
                {
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubRoles", x => new { x.ClubId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ClubRoles_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubRoles_UserId",
                table: "ClubRoles",
                column: "UserId");

            // Managers were managers of every Club, so they become a manager of each one there is:
            // nobody loses access (#110). With no Club there is nothing to manage, and they are made
            // managers of the first by whoever creates it. The flag (1) then goes.
            migrationBuilder.Sql(
                """
                INSERT INTO "ClubRoles" ("ClubId", "UserId", "Kind", "GrantedAt")
                SELECT c."Id", u."Id", 1, now()
                FROM "Users" u CROSS JOIN "Clubs" c
                WHERE (u."Roles" & 1) = 1;

                UPDATE "Users" SET "Roles" = "Roles" & ~1 WHERE ("Roles" & 1) = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A manager of any Club was a manager of them all.
            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Roles" = "Roles" | 1
                WHERE "Id" IN (SELECT "UserId" FROM "ClubRoles" WHERE "Kind" = 1);
                """);

            migrationBuilder.DropTable(
                name: "ClubRoles");
        }
    }
}
