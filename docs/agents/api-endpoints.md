# API Endpoints

How endpoints are structured in `api/src/SocialTennis.Api/`. The reasoning is ADR-0010; this is the recipe.

**One endpoint is one static class in one file, holding only its handler.** Routes are declared in the feature's endpoints class, never inside the endpoint file, and never as lambdas.

## Layout

```
api/src/SocialTennis.Api/
├── Features/<Feature>/
│   ├── <Feature>Endpoints.cs   routing table: MapGroup + every route, verb, filter, policy
│   ├── Contracts.cs            request/response records for the feature
│   └── <EndpointName>.cs       one file per endpoint — HandleAsync only
├── Validation/                 IValidatable, ValidationFilter<T>, ValidatesBody<T>()
├── Authentication/             scheme handler, AuthOptions, Tokens, sender seam
├── Data/  Domain/  Migrations/
```

`Features/<Feature>/` holds the vertical slice only. Anything cross-cutting — an authentication scheme, an options class, a crypto helper — is infrastructure and lives in its own top-level folder. Namespaces follow folders.

## Adding an endpoint

1. **New file** `Features/<Feature>/<EndpointName>.cs`, containing a static class with **one member: `HandleAsync`**. The class name is the endpoint name and matches the file name. No route information lives here — no verb, no path, no filters, no `WithName`.
2. **Contracts** go in that feature's `Contracts.cs`, not in the endpoint file.
3. **`HandleAsync`** holds the logic and declares its dependencies as parameters — `TennisDbContext`, options, `CancellationToken` — bound by ASP.NET. Do not add a service layer for a single caller; extract one only when a second caller appears.
4. **Declare the route** in `<Feature>Endpoints.cs`, passing the handler as a method group:

   ```csharp
   group.MapPost("/magic-link", RequestMagicLink.HandleAsync)
       .ValidatesBody<MagicLinkRequest>()
       .WithName(nameof(RequestMagicLink));
   ```

   That file is the feature's routing table — the one place to read the whole HTTP surface. Registration is explicit; nothing is discovered by scanning.
5. **Integration test** in `SocialTennis.Api.IntegrationTests`, over HTTP.
6. **Regenerate the TS client** and commit the result: `docker compose run --rm web npm run api:generate`.

A new feature also needs a `<Feature>Endpoints.cs` with its `MapGroup` prefix, and one line in `Program.cs`. Registration order in `Program.cs` determines path order in the OpenAPI document and therefore in the generated client — append rather than reorder, so client diffs stay meaningful.

## Return types

Return `TypedResults` unions — `Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>>` — not `IResult`. The union is what puts response codes and schemas into the OpenAPI document, and therefore into the generated client. A handler returning `IResult` produces an endpoint the TS client knows nothing about.

## Validation

Shape validation goes in the filter, not the handler. Implement `IValidatable` on the request record:

```csharp
public record MagicLinkRequest(string Email) : IValidatable
{
    public Dictionary<string, string[]>? Validate() =>
        Email.Trim() is { Length: > 0 and <= 320 } email && email.Contains('@')
            ? null
            : new Dictionary<string, string[]> { ["email"] = ["A valid email address is required."] };
}
```

and attach it in the routing table with `.ValidatesBody<MagicLinkRequest>()`. Never call `.AddEndpointFilter<ValidationFilter<T>>()` directly — `ValidatesBody` also declares the 400 on the OpenAPI document, and splitting them desyncs the generated client.

What belongs in `Validate()`: shape. Length, format, required-ness, ranges — anything answerable from the request alone. `Validate()` must not mutate.

What does not: anything needing a `DbContext`, configuration, or the current user. Those are business rules for `HandleAsync`. Normalisation — trimming, lower-casing — is not validation and also belongs in `HandleAsync`.

## Naming

The class name is the file name is the `WithName` is the OpenAPI `operationId` is the key in `web/src/lib/api/schema.d.ts`. One identifier the whole way through, so a generated operation name leads straight to the C# file. Always write `.WithName(nameof(TheClass))` rather than a string literal.

## Route groups

The feature's endpoints class owns the prefix. Use the **empty** pattern for an endpoint at the group root — `MapGet("", …)` under `MapGroup("/clubs")` gives `/clubs`, whereas `MapGet("/", …)` gives `/clubs/`.

Put `RequireAuthorization()` on individual endpoints unless *every* endpoint in the group needs it. `/auth` is a group where it can't be hoisted: requesting and redeeming a magic link are necessarily anonymous.

## Testing

Integration tests over HTTP against real Postgres are the default seam, and the only seam for anything touching the database. No in-memory or fake EF provider belongs in this repo.

`SocialTennis.Api.UnitTests` exists for pure logic only. The rule is exactly: **if it needs a `DbContext`, it does not go there.** `Validate()` implementations and helpers like `Tokens` qualify; a handler that queries qualifies for an integration test instead. The project deliberately has no test-host reference, so this can't be worked around by accident.

## Known exception

`GET /clubs` returns the `Club` entity rather than a response contract, so the EF model is currently the public wire format for that endpoint. Don't copy the pattern; new endpoints get contracts. Tracked as issue #28.
