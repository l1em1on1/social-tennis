using Microsoft.AspNetCore.Http.HttpResults;
using SocialTennis.Api.Data;
using SocialTennis.Api.Domain;
using SocialTennis.Api.Features.Clubs.Contracts;

namespace SocialTennis.Api.Features.Clubs;

/// <summary>An Admin creates a Club. A fresh deployment has none until one does.</summary>
public static class CreateClub
{
    public static async Task<Created<CreateClubResponse>> HandleAsync(
        CreateClubRequest request,
        TennisDbContext db,
        CancellationToken cancellationToken)
    {
        var club = new Club { Id = Guid.NewGuid(), Name = request.Name.Trim() };
        db.Clubs.Add(club);
        await db.SaveChangesAsync(cancellationToken);

        // No Location: there is no route that reads a single Club.
        return TypedResults.Created((string?)null, new CreateClubResponse(club.Id, club.Name));
    }
}
