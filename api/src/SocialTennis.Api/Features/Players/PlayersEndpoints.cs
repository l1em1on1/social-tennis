using SocialTennis.Api.Features.Players.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Players;

public static class PlayersEndpoints
{
    /// <summary>
    /// The signed-in User's own Players. Every route here is scoped to the
    /// caller: there is no way to list or change another User's Players.
    /// </summary>
    public static IEndpointRouteBuilder MapPlayersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/players");

        group.MapGet("", GetPlayers.HandleAsync)
            .WithName(nameof(GetPlayers));

        group.MapPost("", CreatePlayer.HandleAsync)
            .ValidatesBody<CreatePlayerRequest, CreatePlayerRequest.Validator>()
            .WithName(nameof(CreatePlayer));

        group.MapPut("/{playerId:guid}", RenamePlayer.HandleAsync)
            .ValidatesBody<RenamePlayerRequest, RenamePlayerRequest.Validator>()
            .WithName(nameof(RenamePlayer));

        return app;
    }
}
