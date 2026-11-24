using SocialTennis.Api.Authentication;

namespace SocialTennis.Api.Features.Users;

public static class UsersEndpoints
{
    /// <summary>
    /// Users and their roles: Admins only, every route. The role is part of the
    /// path, and the constraint is the whole list of roles there are, so an
    /// unknown one is a 404 and the handlers can parse it without checking.
    /// </summary>
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").RequireAuthorization(Policies.Admin);

        group.MapGet("", GetUsers.HandleAsync)
            .WithName(nameof(GetUsers));

        group.MapPut("/{userId:guid}/roles/{role:regex(^(Manager|Admin)$)}", GrantRole.HandleAsync)
            .WithName(nameof(GrantRole));

        group.MapDelete("/{userId:guid}/roles/{role:regex(^(Manager|Admin)$)}", RevokeRole.HandleAsync)
            // Declared by hand: a ProblemHttpResult in a Results<> union
            // carries no status of its own for the OpenAPI document.
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RevokeRole));

        // Deleting a User (#116): their Players become Former players, results intact.
        group.MapDelete("/{userId:guid}", DeleteUser.HandleAsync)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(DeleteUser));

        return app;
    }
}
