using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class UserLastSignedIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSignedInAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSignedInAt",
                table: "Users");
        }
    }
}
