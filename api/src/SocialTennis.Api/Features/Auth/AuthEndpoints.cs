using SocialTennis.Api.Validation;

namespace SocialTennis.Api.Features.Auth;

public static class AuthEndpoints
{
    /// <summary>
    /// The feature's routing table: every route, verb, filter, and policy for
    /// /auth in one readable block. Endpoint classes hold only their handler, so
    /// what the HTTP surface looks like is answered here rather than by opening
    /// four files.
    /// </summary>
    /// <remarks>
    /// Registration is explicit rather than assembly-scanned: handlers are plain
    /// statics with no marker interface, so a scan could only match on method
    /// name and signature and a rename would fail silently at runtime. Passing
    /// HandleAsync as a method group is compiler-checked.
    /// </remarks>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Authorization is per-endpoint, not on the group: requesting and
        // redeeming a link are necessarily anonymous.
        var group = app.MapGroup("/auth");

        group.MapPost("/magic-link", RequestMagicLink.HandleAsync)
            .ValidatesBody<MagicLinkRequest>()
            .WithName(nameof(RequestMagicLink));

        group.MapPost("/sessions", RedeemMagicLink.HandleAsync)
            .WithName(nameof(RedeemMagicLink));

        group.MapGet("/me", GetCurrentUser.HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(GetCurrentUser));

        group.MapDelete("/sessions/current", Logout.HandleAsync)
            .RequireAuthorization()
            .WithName(nameof(Logout));

        return app;
    }
}
