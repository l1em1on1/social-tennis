using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Leagues;

public static class LeaguesEndpoints
{
    /// <summary>
    /// Leagues (issue #5). Creating, adding Players and deciding Join Requests
    /// are a Manager's, club-wide in v1 (ADR-0003). Asking to join and listing
    /// a Player's Leagues are the Player holder's.
    /// </summary>
    public static IEndpointRouteBuilder MapLeaguesEndpoints(this IEndpointRouteBuilder app)
    {
        // The 409s are declared by hand: a ProblemHttpResult in a Results<>
        // union carries no status of its own for the OpenAPI document.
        var club = app.MapGroup("/clubs/{clubId:guid}/leagues");

        club.MapGet("", GetClubLeagues.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(GetClubLeagues));

        club.MapPost("", CreateLeague.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<CreateLeagueRequest, CreateLeagueRequest.Validator>()
            .WithName(nameof(CreateLeague));

        var league = app.MapGroup("/leagues/{leagueId:guid}");

        league.MapGet("", GetLeague.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(GetLeague));

        league.MapPut("/participants/{playerId:guid}", AddLeagueParticipant.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AddLeagueParticipant));

        // The Player's side: any signed-in User, for a Player they hold.
        league.MapPost("/join-requests", AskToJoinLeague.HandleAsync)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AskToJoinLeague));

        league.MapPut("/join-requests/{playerId:guid}/approval", ApproveLeagueJoinRequest.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ApproveLeagueJoinRequest));

        league.MapPut("/join-requests/{playerId:guid}/rejection", RejectLeagueJoinRequest.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RejectLeagueJoinRequest));

        app.MapGet("/players/{playerId:guid}/leagues", GetPlayerLeagues.HandleAsync)
            .WithName(nameof(GetPlayerLeagues));

        return app;
    }
}
