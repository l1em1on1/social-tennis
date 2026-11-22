using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Games.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Games;

/// <summary>A League Game and its Score (#38): entered, corrected and overridden.</summary>
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

        // Corrected by a Player who played it, while its Stage is open.
        game.MapPut("/score", CorrectScore.HandleAsync)
            .ValidatesBody<CorrectScoreRequest, CorrectScoreRequest.Validator>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CorrectScore));

        // Overridden by a Manager, open Stage or closed.
        game.MapPut("/score/override", OverrideScore.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<OverrideScoreRequest, OverrideScoreRequest.Validator>()
            // The policy's refusal of anyone else, declared so the client knows it.
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(OverrideScore));

        return app;
    }
}
