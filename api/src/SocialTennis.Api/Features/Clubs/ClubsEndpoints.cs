namespace SocialTennis.Api.Features.Clubs;

public static class ClubsEndpoints
{
    public static IEndpointRouteBuilder MapClubsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/clubs");

        // Empty pattern, not "/": the group prefix is the whole route, and "/"
        // would combine to "/clubs/".
        group.MapGet("", GetClubs.HandleAsync)
            .WithName(nameof(GetClubs));

        return app;
    }
}
