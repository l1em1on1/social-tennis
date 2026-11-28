using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Players;

public static class PlayersEndpoints
{
    /// <summary>
    /// The signed-in User's own Players. Every route but the Rating one is
    /// scoped to the caller: there is no way to list another User's Players or
    /// change their Name, Gender or Level.
    /// </summary>
    public static IEndpointRouteBuilder MapPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/players");

        group.MapGet("", GetPlayers.HandleAsync)
            .WithName(nameof(GetPlayers));

        group.MapPost("", CreatePlayer.HandleAsync)
            .ValidatesBody<CreatePlayerRequest, CreatePlayerRequest.Validator>()
            // Declared by hand: a ProblemHttpResult in a Results<> union
            // carries no status of its own for the OpenAPI document.
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CreatePlayer));

        group.MapPut("/{playerId:guid}", UpdatePlayer.HandleAsync)
            .ValidatesBody<UpdatePlayerRequest, UpdatePlayerRequest.Validator>()
            .WithName(nameof(UpdatePlayer));

        // A Player's usual week (#276): when they're usually free, in every League; the holder's alone.
        group.MapGet("/{playerId:guid}/usual-week", GetUsualWeek.HandleAsync)
            .WithName(nameof(GetUsualWeek));

        group.MapPut("/{playerId:guid}/usual-week", SetUsualWeek.HandleAsync)
            .ValidatesBody<SetUsualWeekRequest, SetUsualWeekRequest.Validator>()
            .WithName(nameof(SetUsualWeek));

        // The one route here that isn't the holder's: Rating is a Manager's to
        // set, for a Player in a Club they run.
        group.MapPut("/{playerId:guid}/rating", SetPlayerRating.HandleAsync)
            .RequireAuthorization(Policies.PlayerManager)
            .ValidatesBody<SetPlayerRatingRequest, SetPlayerRatingRequest.Validator>()
            .WithName(nameof(SetPlayerRating));

        // The Player's own way out. A Manager removing them is in ClubsEndpoints.
        group.MapDelete("/{playerId:guid}/clubs/{clubId:guid}", LeaveClub.HandleAsync)
            .WithName(nameof(LeaveClub));

        return app;
    }
}
