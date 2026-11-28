using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Leagues.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Leagues;

public static class LeaguesEndpoints
{
    /// <summary>
    /// Leagues (issue #5). Creating, adding Players and deciding Join Requests
    /// are a Manager's, by the policy each route names (ADR-0017). Asking to join, taking a
    /// request back, listing partners and listing a Player's Leagues are the
    /// Player holder's.
    /// </summary>
    public static IEndpointRouteBuilder MapLeaguesEndpoints(this IEndpointRouteBuilder app)
    {
        // The 409s are declared by hand: a ProblemHttpResult in a Results<>
        // union carries no status of its own for the OpenAPI document.
        var club = app.MapGroup("/clubs/{clubId:guid}/leagues");

        club.MapGet("", GetClubLeagues.HandleAsync)
            .RequireAuthorization(Policies.ClubStaff)
            .WithName(nameof(GetClubLeagues));

        club.MapPost("", CreateLeague.HandleAsync)
            .RequireAuthorization(Policies.LeagueCreator)
            .ValidatesBody<CreateLeagueRequest, CreateLeagueRequest.Validator>()
            .WithName(nameof(CreateLeague));

        var league = app.MapGroup("/leagues/{leagueId:guid}");

        league.MapGet("", GetLeague.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .WithName(nameof(GetLeague));

        league.MapPut("", RenameLeague.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<RenameLeagueRequest, RenameLeagueRequest.Validator>()
            .WithName(nameof(RenameLeague));

        // Who runs it (#220): a manager of its Club to say.
        league.MapPut("/managers/{userId:guid}", AssignLeagueManager.HandleAsync)
            .RequireAuthorization(Policies.LeagueClubManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AssignLeagueManager));

        league.MapDelete("/managers/{userId:guid}", UnassignLeagueManager.HandleAsync)
            .RequireAuthorization(Policies.LeagueClubManager)
            .WithName(nameof(UnassignLeagueManager));

        // Who could be added (#220): the Club's members, by name.
        league.MapGet("/addable-players", GetAddablePlayers.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .WithName(nameof(GetAddablePlayers));

        league.MapPut("/participants/{playerId:guid}", AddLeagueParticipant.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AddLeagueParticipant));

        league.MapDelete("/participants/{playerId:guid}", RemoveLeagueParticipant.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RemoveLeagueParticipant));

        league.MapPost("/teams", PairTeam.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<PairTeamRequest, PairTeamRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(PairTeam));

        league.MapDelete("/teams/{teamId:guid}", UnpairTeam.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(UnpairTeam));

        // Starting a Divisional League (#7): the proposal, then the Manager's confirmed ladder.
        league.MapGet("/start", GetLeagueStart.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(GetLeagueStart));

        league.MapPost("/start", StartLeague.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<StartLeagueRequest, StartLeagueRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(StartLeague));

        // Closing the current Stage (#10): its screen, and the close.
        league.MapGet("/close", GetStageClose.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(GetStageClose));

        league.MapPost("/close", CloseStage.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<CloseStageRequest, CloseStageRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(CloseStage));

        // A Manager's mid-Stage move of a Team to another Division (#10): not Relegation's path.
        league.MapPost("/moves", MoveTeam.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<MoveTeamRequest, MoveTeamRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(MoveTeam));

        // The ladder (#7): the League's Players and Managers; anyone else, a 404.
        league.MapGet("/ladder", GetLeagueLadder.HandleAsync)
            .WithName(nameof(GetLeagueLadder));

        // A Foursomes tie the Division's Players settle (#14 part 3): a Player they hold, in that Division.
        league.MapPut("/tie-marks", MarkTie.HandleAsync)
            .ValidatesBody<MarkTieRequest, MarkTieRequest.Validator>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(MarkTie));

        // Withdrawal (#21): a Player asks for a Player they hold, and cancels while it waits; the Manager decides,
        // or withdraws a Team directly.
        league.MapPost("/withdrawals", AskToWithdraw.HandleAsync)
            .ValidatesBody<AskToWithdrawRequest, AskToWithdrawRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(AskToWithdraw));

        league.MapDelete("/players/{playerId:guid}/withdrawal", CancelWithdrawal.HandleAsync)
            .WithName(nameof(CancelWithdrawal));

        league.MapPut("/withdrawals/{requestId:guid}", DecideWithdrawal.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ValidatesBody<DecideWithdrawalRequest, DecideWithdrawalRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(DecideWithdrawal));

        league.MapPut("/teams/{teamId:guid}/withdrawal", WithdrawTeam.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(WithdrawTeam));

        // A Player's free days this Stage (#9): a Player the User holds, in the League; theirs alone.
        league.MapGet("/players/{playerId:guid}/availability", GetAvailability.HandleAsync)
            .WithName(nameof(GetAvailability));

        league.MapPut("/players/{playerId:guid}/availability", SetAvailability.HandleAsync)
            .ValidatesBody<SetAvailabilityRequest, SetAvailabilityRequest.Validator>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SetAvailability));

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
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ApproveLeagueJoinRequest));

        league.MapPut("/join-requests/{playerId:guid}/rejection", RejectLeagueJoinRequest.HandleAsync)
            .RequireAuthorization(Policies.LeagueManager)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RejectLeagueJoinRequest));

        app.MapGet("/players/{playerId:guid}/leagues", GetPlayerLeagues.HandleAsync)
            .WithName(nameof(GetPlayerLeagues));

        // Home (#42): the Player's Games this Stage, by League.
        app.MapGet("/players/{playerId:guid}/games", GetPlayerGames.HandleAsync)
            .WithName(nameof(GetPlayerGames));

        return app;
    }
}
