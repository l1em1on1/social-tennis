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
├── Contracts/                  contracts shared across features — PageInfo only
├── Validation/                 ValidationFilter<TRequest, TValidator>, ValidatesBody<,>()
├── Authentication/             scheme handler, AuthOptions, Tokens, sender seam
├── Data/  Domain/  Migrations/
```

`Features/<Feature>/` holds the vertical slice only. Anything cross-cutting — an authentication scheme, an options class, a crypto helper — is infrastructure and lives in its own top-level folder.

The top-level `Contracts/` is the narrow exception to "contracts are feature-scoped", and it should stay narrow: a record earns a place there only when it is identical across features by construction, as `PageInfo` is. A contract that merely *happens* to look the same in two features belongs to each of them separately — that's the same payload-independence argument ADR-0012 makes for list elements.

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

Return `TypedResults` unions — `Task<Results<Ok<RedeemMagicLinkResponse>, UnauthorizedHttpResult>>` — not `IResult`. The union is what puts response codes and schemas into the OpenAPI document, and therefore into the generated client. A handler returning `IResult` produces an endpoint the TS client knows nothing about.

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

**Contracts follow the same principle: a contract is named for the endpoint it serves, plus `Request` or `Response`** — `GetCurrentUserResponse`, `RedeemMagicLinkRequest`. Record names *are* OpenAPI schema names, so this is what a web-side reader sees; a contract named for anything else forces them to guess which endpoint produced it.

One carve-out: **don't stutter.** When the endpoint name already contains the direction word, drop the duplicate — `RequestMagicLink` takes `MagicLinkRequest`, not `RequestMagicLinkRequest`.

Renaming a contract is a **wire-visible change**: the OpenAPI schema key moves, so `npm run api:generate` and the regenerated `schema.d.ts` belong in the same commit as the rename.

## List endpoints

**A list endpoint returns an envelope, never a bare array** (reasoning: ADR-0012). The envelope is named for its endpoint like any other contract; its collection property and element type are named for the resource:

```csharp
public record GetClubsResponse(IReadOnlyList<ClubSummary> Clubs, PageInfo Page);
```

Three rules:

- **The collection property is domain-named** — `Clubs`, not `Items`. Envelopes are hand-written per endpoint rather than shared from a generic base, so each list can carry its own list-level stats (an open-for-joining count, a last-updated timestamp) without every other list inheriting a nullable field it has no use for.
- **`Page` is mandatory**, including on lists that can never grow. `PageInfo` is shared, from `SocialTennis.Api.Contracts` — the only contract that isn't feature-scoped. `Total` means *items matching the query, ignoring any paging window*; document it that way rather than as "how many I returned", so paging changes the value and not the meaning.
- **The element is its own record, independent of any single-item response.** `GetClubs` returns `ClubSummary`; a later `GET /clubs/{id}` gets its own `ClubDetails` rather than reusing it. They answer different questions and are meant to diverge — a richer detail view must never silently widen every row of a long list.

`OpenApiContractTests.No_endpoint_returns_a_bare_array` enforces this against the generated document. It is the only thing that does: a base record or marker interface can make envelopes uniform, but neither can stop a new handler returning `List<T>` directly.

Note for anyone tempted to share shape via inheritance: **it doesn't reach the wire.** A derived record is emitted as a flat schema with every inherited property inlined, and the base type never appears in `components.schemas` — ASP.NET Core composes schemas only for `[JsonPolymorphic]`/`[JsonDerivedType]` hierarchies. Positional records don't inherit for free either, so the base ends up re-declaring every parameter.

## Route groups

The feature's endpoints class owns the prefix. Use the **empty** pattern for an endpoint at the group root — `MapGet("", …)` under `MapGroup("/clubs")` gives `/clubs`, whereas `MapGet("/", …)` gives `/clubs/`.

Put `RequireAuthorization()` on individual endpoints unless *every* endpoint in the group needs it. `/auth` is a group where it can't be hoisted: requesting and redeeming a magic link are necessarily anonymous.

## Testing

Integration tests over HTTP against real Postgres are the default seam, and the only seam for anything touching the database. No in-memory or fake EF provider belongs in this repo.

`SocialTennis.Api.UnitTests` exists for pure logic only. The rule is exactly: **if it needs a `DbContext`, it does not go there.** Validators and helpers like `Tokens` qualify; a handler that queries qualifies for an integration test instead. The project deliberately has no test-host reference, so this can't be worked around by accident.

Validator tests use FluentValidation's `TestHelper`, asserting on the **wire** field name rather than the property expression — `.OverridePropertyName` decouples the two, so `ShouldHaveValidationErrorFor("email")` matches while `ShouldHaveValidationErrorFor(r => r.Email)` does not.
