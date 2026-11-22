using Microsoft.AspNetCore.Http.HttpResults;

namespace SocialTennis.Api.Features.Games;

/// <summary>The refusals the Score endpoints share (#38), each with its stable <c>type</c> (ADR-0017).</summary>
public static class GameScores
{
    public static ProblemHttpResult NotInGame() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            type: "not-in-game",
            title: "Only a Player in this game can enter its score.");

    public static ProblemHttpResult AlreadyScored() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "already-scored",
            title: "This game already has a score.");
}
