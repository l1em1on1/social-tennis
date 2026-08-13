# API Endpoints

How endpoints are structured in `api/src/SocialTennis.Api/`. The reasoning is ADR-0010; this is the recipe.

**One endpoint is one static class in one file, holding only its handler.** Routes are declared in the feature's endpoints class, never inside the endpoint file, and never as lambdas.

## Layout

```
api/src/SocialTennis.Api/
├── Features/<Feature>/
│   ├── <Feature>Endpoints.cs   routing table: MapGroup + every route, verb, filter, policy
│   ├── <EndpointName>.cs       one file per endpoint — HandleAsync only
│   └── Contracts/
│       └── <RecordName>.cs     one file per request/response record
├── Validation/                 ValidationFilter<TRequest, TValidator>, ValidatesBody<,>()
├── Authentication/             scheme handler, AuthOptions, Tokens, sender seam
├── Data/  Domain/  Migrations/
```

`Features/<Feature>/` holds the vertical slice only. Anything cross-cutting — an authentication scheme, an options class, a crypto helper — is infrastructure and lives in its own top-level folder.

**Namespaces follow folders**, including `Contracts/`: a record in `Features/Auth/Contracts/` is in `SocialTennis.Api.Features.Auth.Contracts`, so endpoint files that name a contract carry `using SocialTennis.Api.Features.Auth.Contracts;`. The folder's top level stays exactly the HTTP surface — the routing table plus one file per endpoint — which is what makes "what does this feature expose?" answerable by listing a directory.

## Adding an endpoint

1. **New file** `Features/<Feature>/<EndpointName>.cs`, containing a static class with **one member: `HandleAsync`**. The class name is the endpoint name and matches the file name. No route information lives here — no verb, no path, no filters, no `WithName`.
2. **Contracts** go one-per-file in that feature's `Contracts/` folder, never in the endpoint file. The record name is the file name. The folder is unconditional — a feature with a single contract still gets it, so the layout never depends on a headcount.
3. **`HandleAsync`** holds the logic and declares its dependencies as parameters — `TennisDbContext`, options, `CancellationToken` — bound by ASP.NET. Do not add a service layer for a single caller; extract one only when a second caller appears.
4. **Declare the route** in `<Feature>Endpoints.cs`, passing the handler as a method group:

   ```csharp
   group.MapPost("/magic-link", RequestMagicLink.HandleAsync)
       .ValidatesBody<MagicLinkRequest, MagicLinkRequest.Validator>()
       .WithName(nameof(RequestMagicLink));
   ```

   That file is the feature's routing table — the one place to read the whole HTTP surface. Registration is explicit; nothing is discovered by scanning.
5. **Integration test** in `SocialTennis.Api.IntegrationTests`, over HTTP.
6. **Regenerate the TS client** and commit the result: `docker compose run --rm web npm run api:generate`.

A new feature also needs a `<Feature>Endpoints.cs` with its `MapGroup` prefix, and one line in `Program.cs`. Registration order in `Program.cs` determines path order in the OpenAPI document and therefore in the generated client — append rather than reorder, so client diffs stay meaningful.

## Return types

Return `TypedResults` unions — `Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>>` — not `IResult`. The union is what puts response codes and schemas into the OpenAPI document, and therefore into the generated client. A handler returning `IResult` produces an endpoint the TS client knows nothing about.

## Validation

Shape validation goes in the filter, not the handler, and uses **FluentValidation** (reasoning: ADR-0011). The validator is a `Validator` class nested in the request record, in that record's own file under `Contracts/`:

```csharp
public record MagicLinkRequest(string Email)
{
    public sealed class Validator : AbstractValidator<MagicLinkRequest>
    {
        public Validator() =>
            RuleFor(request => request.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("An email address is required.")
                .Must(email => email.Trim() is { Length: <= 320 } trimmed && trimmed.Contains('@'))
                .WithMessage("A valid email address is required.")
                .OverridePropertyName("email");
    }
}
```

Attach it in the routing table with `.ValidatesBody<MagicLinkRequest, MagicLinkRequest.Validator>()`. Both type parameters are required and the constraint is compiler-checked, so a contract marked for validation with no matching validator does not build. Never call `.AddEndpointFilter<ValidationFilter<,>>()` directly — `ValidatesBody` also declares the 400 on the OpenAPI document, and splitting them desyncs the generated client.

Three rules that are not negotiable:

- **`.OverridePropertyName("wireName")` on every rule.** FluentValidation names errors after the CLR property, so without it the client sees `"Email"` where it expects `"email"`. `HttpValidationProblemDetails.errors` is a free-form dictionary, so TypeScript cannot catch the mistake.
- **Validators take no constructor parameters.** The `new()` constraint enforces it: anything needing a `DbContext`, configuration, or the current user is a business rule for `HandleAsync`.
- **Validators must not mutate.** Normalisation — trimming, lower-casing — is not validation and belongs in `HandleAsync`. Trimming *to decide acceptability*, as above, is fine.

What belongs here: length, format, required-ness, ranges — anything answerable from the request alone.

## Naming

The class name is the file name is the `WithName` is the OpenAPI `operationId` is the key in `web/src/lib/api/schema.d.ts`. One identifier the whole way through, so a generated operation name leads straight to the C# file. Always write `.WithName(nameof(TheClass))` rather than a string literal.

## Route groups

The feature's endpoints class owns the prefix. Use the **empty** pattern for an endpoint at the group root — `MapGet("", …)` under `MapGroup("/clubs")` gives `/clubs`, whereas `MapGet("/", …)` gives `/clubs/`.

Put `RequireAuthorization()` on individual endpoints unless *every* endpoint in the group needs it. `/auth` is a group where it can't be hoisted: requesting and redeeming a magic link are necessarily anonymous.

## Testing

Integration tests over HTTP against real Postgres are the default seam, and the only seam for anything touching the database. No in-memory or fake EF provider belongs in this repo.

`SocialTennis.Api.UnitTests` exists for pure logic only. The rule is exactly: **if it needs a `DbContext`, it does not go there.** Validators and helpers like `Tokens` qualify; a handler that queries qualifies for an integration test instead. The project deliberately has no test-host reference, so this can't be worked around by accident.

Validator tests use FluentValidation's `TestHelper`, asserting on the **wire** field name rather than the property expression — `.OverridePropertyName` decouples the two, so `ShouldHaveValidationErrorFor("email")` matches while `ShouldHaveValidationErrorFor(r => r.Email)` does not.
