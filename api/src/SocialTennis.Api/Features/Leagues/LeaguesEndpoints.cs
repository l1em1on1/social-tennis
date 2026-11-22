using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Leagues;

public static class LeaguesEndpoints
{
    /// <summary>
    /// Leagues (issue #5). Creating, adding Players and deciding Join Requests
    /// are a Manager's, club-wide in v1 (ADR-0003). Asking to join, taking a
    /// request back, listing partners and listing a Player's Leagues are the
    /// Player holder's.
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

        league.MapPut("", RenameLeague.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<RenameLeagueRequest, RenameLeagueRequest.Validator>()
            .WithName(nameof(RenameLeague));

        league.MapPut("/participants/{playerId:guid}", AddLeagueParticipant.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AddLeagueParticipant));

        league.MapDelete("/participants/{playerId:guid}", RemoveLeagueParticipant.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RemoveLeagueParticipant));

        league.MapPost("/teams", PairTeam.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<PairTeamRequest, PairTeamRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(PairTeam));

        league.MapDelete("/teams/{teamId:guid}", UnpairTeam.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(UnpairTeam));

        // Starting a Divisional League (#7): the proposal, then the Manager's confirmed ladder.
        league.MapGet("/start", GetLeagueStart.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(GetLeagueStart));

        league.MapPost("/start", StartLeague.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<StartLeagueRequest, StartLeagueRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(StartLeague));

        // Closing the current Stage (#10): its screen, and the close.
        league.MapGet("/close", GetStageClose.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(GetStageClose));

        league.MapPost("/close", CloseStage.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<CloseStageRequest, CloseStageRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CloseStage));

        // The ladder (#7): the League's Players and Managers; anyone else, a 404.
        league.MapGet("/ladder", GetLeagueLadder.HandleAsync)
            .WithName(nameof(GetLeagueLadder));

        // The Player's side: any signed-in User, for a Player they hold.
        league.MapPost("/join-requests", AskToJoinLeague.HandleAsync)
            .ValidatesBody<AskToJoinLeagueRequest, AskToJoinLeagueRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AskToJoinLeague));

        // Taken back by the Player who asked or the partner named (#6).
        league.MapDelete("/join-requests/{playerId:guid}", TakeBackLeagueJoinRequest.HandleAsync)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(TakeBackLeagueJoinRequest));

        // Who a Player could ask to join a doubles League with (#6): ?playerId= names the Player.
        league.MapGet("/partners", GetLeaguePartners.HandleAsync)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(GetLeaguePartners));

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
