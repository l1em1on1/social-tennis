using SocialTennis.Api.Authentication;
using SocialTennis.Api.Features.Clubs.Contracts;
using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Clubs;

public static class ClubsEndpoints
{
    public static IEndpointRouteBuilder MapClubsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/clubs");

        // Empty pattern, not "/": the group prefix is the whole route, and "/"
        // would combine to "/clubs/".
        // Managers' and Admins' only: a Player gets into a Club through its join link (#222).
        group.MapGet("", GetClubs.HandleAsync)
            .RequireAuthorization(Policies.AnyManager)
            .WithName(nameof(GetClubs));

        group.MapPost("", CreateClub.HandleAsync)
            .RequireAuthorization(Policies.Admin)
            .ValidatesBody<CreateClubRequest, CreateClubRequest.Validator>()
            .WithName(nameof(CreateClub));

        group.MapPut("/{clubId:guid}", RenameClub.HandleAsync)
            .RequireAuthorization(Policies.Admin)
            .ValidatesBody<RenameClubRequest, RenameClubRequest.Validator>()
            .WithName(nameof(RenameClub));

        // A Manager's side of membership.
        group.MapGet("/{clubId:guid}/members", GetClubMembers.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(GetClubMembers));

        group.MapPut("/{clubId:guid}/members/{playerId:guid}/approval", ApproveClubMember.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(ApproveClubMember));

        group.MapDelete("/{clubId:guid}/members/{playerId:guid}", RemoveClubMember.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(RemoveClubMember));

        group.MapPost("/{clubId:guid}/players", AddClubPlayer.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .ValidatesBody<AddClubPlayerRequest, AddClubPlayerRequest.Validator>()
            .WithName(nameof(AddClubPlayer));

        group.MapPost("/{clubId:guid}/invites", InviteToClub.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .ValidatesBody<InviteToClubRequest, InviteToClubRequest.Validator>()
            .WithName(nameof(InviteToClub));

        group.MapDelete("/{clubId:guid}/invites/{inviteId:guid}", WithdrawClubInvite.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(WithdrawClubInvite));

        // The Club's join link (#222, ADR-0032): PUT turns it on and keeps a working code; reset makes a new one.
        group.MapGet("/{clubId:guid}/join-link", GetJoinLink.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(GetJoinLink));

        group.MapPut("/{clubId:guid}/join-link", TurnOnJoinLink.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(TurnOnJoinLink));

        group.MapPost("/{clubId:guid}/join-link/reset", ResetJoinLink.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(ResetJoinLink));

        group.MapDelete("/{clubId:guid}/join-link", TurnOffJoinLink.HandleAsync)
            .RequireAuthorization(Policies.ClubManager)
            .WithName(nameof(TurnOffJoinLink));

        // A Player's side: the link names its Club, and asking through it waits for a Manager.
        var links = app.MapGroup("/join-links");

        links.MapGet("/{code}", GetJoinLinkClub.HandleAsync)
            .WithName(nameof(GetJoinLinkClub));

        links.MapPut("/{code}/members/{playerId:guid}", JoinClub.HandleAsync)
            .WithName(nameof(JoinClub));

        return app;
    }
}
