using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialTennis.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeededClub : Migration
    {
        /// <summary>
        /// The walking skeleton seeded one Club, "Social Tennis Club". Clubs are
        /// now created by an Admin (issue #18), so a fresh deployment should
        /// start with none.
        /// </summary>
        /// <remarks>
        /// Hand-written, not the generated DeleteData: by the time this runs on
        /// a deployed database the seeded Club may be in real use, and deleting
        /// it would cascade to its members, requests and invites. So it is
        /// deleted only while nobody is in it or invited to it. Where it
        /// survives, an Admin renames it (PUT /clubs/{clubId}).
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Clubs" c
                WHERE c."Id" = '6edb14f9-1b3d-4a63-b998-0d5757a5c8f1'
                  AND NOT EXISTS (SELECT 1 FROM "ClubMemberships" m WHERE m."ClubId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM "ClubInvites" i WHERE i."ClubId" = c."Id");
                """);
        }

        /// <summary>Nothing to undo: the model no longer seeds a Club, and a kept one is real data.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
