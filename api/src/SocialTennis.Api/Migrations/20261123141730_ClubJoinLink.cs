using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClubJoinLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JoinCode",
                table: "Clubs",
                type: "character varying(22)",
                maxLength: 22,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_JoinCode",
                table: "Clubs",
                column: "JoinCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clubs_JoinCode",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "JoinCode",
                table: "Clubs");
        }
    }
}
