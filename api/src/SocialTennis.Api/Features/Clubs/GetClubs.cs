using Microsoft.EntityFrameworkCore;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;

namespace SocialTennis.Api.Features.Clubs;

public static class GetClubs
{
    // Returns the Club entity rather than a response contract — the one place
    // the wire format is still coupled to the EF model. Deliberately left as-is
    // by the endpoint restructure so the existing contract is unchanged; the
    // ClubResponse DTO lands separately — issue #28.
    public static async Task<List<Club>> HandleAsync(
        TennisDbContext db,
        CancellationToken cancellationToken) =>
        await db.Clubs.OrderBy(c => c.Name).ToListAsync(cancellationToken);
}
