using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Games;

/// <summary>A League Game and its Score (#38).</summary>
public static class GamesEndpoints
{
    public static IEndpointRouteBuilder MapGamesEndpoints(this IEndpointRouteBuilder app)
    {
        var game = app.MapGroup("/games/{gameId:guid}");

        // Its Match screen: the League's Players and Managers; anyone else, a 404.
        game.MapGet("", GetGame.HandleAsync)
            .WithName(nameof(GetGame));

        // Entered by a Player who played it, for a Player the User holds.
        game.MapPost("/score", SubmitScore.HandleAsync)
            .ValidatesBody<SubmitScoreRequest, SubmitScoreRequest.Validator>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SubmitScore));

        return app;
    }
}
