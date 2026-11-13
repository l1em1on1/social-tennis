using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
/// the behaviour. The response is declared with a ProblemDetails body, which
/// is what UseStatusCodePages writes for the middleware's bodiless 401.
/// </remarks>
internal sealed class UnauthorizedResponseTransformer : IOpenApiOperationTransformer
{
    private const string ProblemSchemaId = nameof(ProblemDetails);

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var anonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (anonymous)
        {
            return;
        }

        // Generated the way the framework generates every other schema, and
        // registered once, so each 401 is a $ref to the same component
        // (ASP.NET Core docs, "generating OpenApiSchemas in transformers").
        var document = context.Document;
        if (document?.Components?.Schemas?.ContainsKey(ProblemSchemaId) != true)
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken);
            document?.AddComponent(ProblemSchemaId, schema);
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd(
            StatusCodes.Status401Unauthorized.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse
            {
                Description = "Unauthorized",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new()
                    {
                        Schema = new OpenApiSchemaReference(ProblemSchemaId, document),
                    },
                },
            });
    }
}
