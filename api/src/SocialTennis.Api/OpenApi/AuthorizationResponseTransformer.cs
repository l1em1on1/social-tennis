using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SocialTennis.Api.OpenApi;

/// <summary>
/// Declares what authorization answers on every operation, so it reaches the
/// generated TS client (ADR-0017): the 401 unless it is anonymous, and the 403
/// if its route names a policy.
/// </summary>
/// <remarks>
/// The API denies by default: the fallback policy requires a signed-in User
/// unless an endpoint says <c>AllowAnonymous()</c>. These answers come from the
/// authorization middleware, not from a handler's <c>Results&lt;&gt;</c> union,
/// so nothing would otherwise put them in the document. Deriving them here from
/// the same metadata the middleware reads means the document can't disagree
/// with the behaviour. Each is declared with a ProblemDetails body, which is
/// what UseStatusCodePages writes for the middleware's bodiless answers.
/// </remarks>
internal sealed class AuthorizationResponseTransformer : IOpenApiOperationTransformer
{
    private const string ProblemSchemaId = nameof(ProblemDetails);

    public async Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        // Generated the way the framework generates every other schema, and
        // registered once, so each answer is a $ref to the same component
        // (ASP.NET Core docs, "generating OpenApiSchemas in transformers").
        var document = context.Document;
        if (document?.Components?.Schemas?.ContainsKey(ProblemSchemaId) != true)
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken);
            document?.AddComponent(ProblemSchemaId, schema);
        }

        Declare(operation, document, StatusCodes.Status401Unauthorized, "Unauthorized");
        if (metadata.OfType<IAuthorizeData>().Any(authorize => authorize.Policy is not null))
        {
            Declare(operation, document, StatusCodes.Status403Forbidden, "Forbidden");
        }
    }

    private static void Declare(OpenApiOperation operation, OpenApiDocument? document, int status, string description)
    {
        operation.Responses ??= [];
        operation.Responses.TryAdd(
            status.ToString(CultureInfo.InvariantCulture),
            new OpenApiResponse
            {
                Description = description,
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
