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
        group.MapGet("", GetClubs.HandleAsync)
            .WithName(nameof(GetClubs));

        group.MapPost("", CreateClub.HandleAsync)
            .RequireAuthorization(Policies.Admin)
            .ValidatesBody<CreateClubRequest, CreateClubRequest.Validator>()
            .WithName(nameof(CreateClub));

        group.MapPut("/{clubId:guid}", RenameClub.HandleAsync)
            .RequireAuthorization(Policies.Admin)
            .ValidatesBody<RenameClubRequest, RenameClubRequest.Validator>()
            .WithName(nameof(RenameClub));

        group.MapPut("/{clubId:guid}/members/{playerId:guid}", JoinClub.HandleAsync)
            .WithName(nameof(JoinClub));

        // A Manager's side of membership.
        group.MapGet("/{clubId:guid}/members", GetClubMembers.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(GetClubMembers));

        group.MapPut("/{clubId:guid}/members/{playerId:guid}/approval", ApproveClubMember.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(ApproveClubMember));

        group.MapDelete("/{clubId:guid}/members/{playerId:guid}", RemoveClubMember.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(RemoveClubMember));

        group.MapPost("/{clubId:guid}/players", AddClubPlayer.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<AddClubPlayerRequest, AddClubPlayerRequest.Validator>()
            .WithName(nameof(AddClubPlayer));

        group.MapPost("/{clubId:guid}/invites", InviteToClub.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .ValidatesBody<InviteToClubRequest, InviteToClubRequest.Validator>()
            .WithName(nameof(InviteToClub));

        group.MapDelete("/{clubId:guid}/invites/{inviteId:guid}", WithdrawClubInvite.HandleAsync)
            .RequireAuthorization(Policies.Manager)
            .WithName(nameof(WithdrawClubInvite));

        return app;
    }
}
