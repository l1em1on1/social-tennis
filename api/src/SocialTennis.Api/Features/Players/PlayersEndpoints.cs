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

        // The one route here that isn't the holder's: Rating is a Manager's to
        // set, for any Player (roles are club-wide in v1, ADR-0003).
        group.MapPut("/{playerId:guid}/rating", SetPlayerRating.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<SetPlayerRatingRequest, SetPlayerRatingRequest.Validator>()
            .WithName(nameof(SetPlayerRating));

        // The Player's own way out. A Manager removing them is in ClubsEndpoints.
        group.MapDelete("/{playerId:guid}/clubs/{clubId:guid}", LeaveClub.HandleAsync)
            .WithName(nameof(LeaveClub));

        return app;
    }
}
