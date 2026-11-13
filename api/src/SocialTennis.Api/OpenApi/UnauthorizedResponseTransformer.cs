using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SocialTennis.Api.OpenApi;

/// <summary>
/// Declares the 401 on every operation that isn't anonymous, so it reaches the
/// generated TS client (ADR-0017).
/// </summary>
/// <remarks>
/// The API denies by default: Program.cs's fallback policy requires a signed-in
/// User unless an endpoint says <c>AllowAnonymous()</c>. That 401 comes from the
/// authorization middleware, not from a handler's <c>Results&lt;&gt;</c> union,
/// so nothing would otherwise put it in the document. Deriving it here from the
/// same metadata the middleware reads means the document can't disagree with
/// the behaviour.
/// </remarks>
internal sealed class UnauthorizedResponseTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var anonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (!anonymous)
        {
            operation.Responses ??= [];
            operation.Responses.TryAdd(
                StatusCodes.Status401Unauthorized.ToString(),
                new OpenApiResponse { Description = "Unauthorized" });
        }

        return Task.CompletedTask;
    }
}
