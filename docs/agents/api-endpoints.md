# API endpoints

How endpoints are built in `api/src/SocialTennis.Api/` (ADR-0010). **One endpoint is one static class in one file, holding only `HandleAsync`.** Routes live in the feature's endpoints class, as method groups.

## Layout

```
api/src/SocialTennis.Api/
├── Features/<Feature>/
│   ├── <Feature>Endpoints.cs   routing table: MapGroup, every route, verb, filter, policy
│   ├── <EndpointName>.cs       one file per endpoint, HandleAsync only
│   └── Contracts/
│       └── <RecordName>.cs     one file per request/response record
├── Contracts/                  shared across features: PageInfo, SetScore
├── Validation/  OpenApi/  Authentication/  Membership/  Roles/
├── Data/  Domain/  Migrations/
```

- `Features/<Feature>/` is the vertical slice: its top level is exactly the HTTP surface. Cross-cutting code (an auth scheme, options, a helper) gets its own top-level folder.
- The top-level `Contracts/` takes only records identical across features by construction, like `PageInfo` (ADR-0012).
- Namespaces follow folders: `Features/Auth/Contracts/` is `SocialTennis.Api.Features.Auth.Contracts`.

## Adding an endpoint

1. **`Features/<Feature>/<EndpointName>.cs`**: a static class named like the file, with one member, `HandleAsync`. No verb, path, filter or `WithName` here.
2. **Contracts**: one record per file in the feature's `Contracts/` folder (always a folder, even for one record), named like the file.
3. **`HandleAsync`** holds the logic and takes its dependencies as parameters (`TennisDbContext`, options, `TimeProvider`, `CancellationToken`). Extract a service only when a second caller appears.
4. **The route**, in `<Feature>Endpoints.cs`, as a method group:

   ```csharp
   group.MapPost("/magic-link", RequestMagicLink.HandleAsync)
       .ValidatesBody<MagicLinkRequest, MagicLinkRequest.Validator>()
       .WithName(nameof(RequestMagicLink));
   ```

5. **An integration test** over HTTP.
6. **Regenerate the TypeScript client** and commit it: README, "Regenerate the TypeScript API client".

A new feature adds `<Feature>Endpoints.cs` with its `MapGroup` prefix and one line in `Program.cs`, appended: registration order is path order in the OpenAPI document and the client.

## Return types

Return `TypedResults` unions, e.g. `Task<Results<Ok<RedeemMagicLinkResponse>, UnauthorizedHttpResult>>`. The union is what puts each status and schema into the OpenAPI document and the client; `IResult` leaves the client blind.

## Validation

FluentValidation, in the filter (ADR-0011). The validator is a `Validator` class nested in the request record, in that record's file:

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

- Attach it with `.ValidatesBody<TRequest, TRequest.Validator>()`, which also declares the 400 in the OpenAPI document. Use it rather than `AddEndpointFilter<ValidationFilter<,>>()`.
- **`.OverridePropertyName("wireName")` on every rule**, so errors carry the JSON name.
- **No constructor parameters** (the `new()` constraint): anything needing the database, configuration or the user is a rule for `HandleAsync`.
- **Validators only check.** Normalising (trimming, lower-casing) happens in `HandleAsync`; trimming to decide acceptability is fine.
- A validator checks what the request alone answers: length, format, required, ranges.

## Naming

- One identifier from file to client: class name = file name = `.WithName(nameof(TheClass))` = OpenAPI `operationId` = the key in `web/src/lib/api/schema.d.ts`.
- A contract is named for its endpoint plus `Request` or `Response` (`GetCurrentUserResponse`), without stuttering: `RequestMagicLink` takes `MagicLinkRequest`.
- Renaming a contract moves its OpenAPI schema key: regenerate the client in the same commit.

## List endpoints

An envelope, never a bare array (ADR-0012), named for its endpoint like any contract:

```csharp
public record GetClubsResponse(IReadOnlyList<ClubSummary> Clubs, PageInfo Page);
```

- The collection is named for the resource (`Clubs`), and each envelope is written by hand, so a list can carry its own list-level fields.
- `Page` (`PageInfo`, shared) is on every list, even one that can't grow. `Total` counts the items matching the query, whatever the paging window.
- The element is its own record (`ClubSummary`), separate from any single-item response, so the two can diverge.
- `OpenApiContractTests.No_endpoint_returns_a_bare_array` enforces the envelope. Sharing shape through record inheritance doesn't reach the wire: the derived schema is emitted flat.

## Route groups

The endpoints class owns the prefix. Map the group root with the empty pattern: `MapGet("", …)` under `MapGroup("/clubs")` is `/clubs` (`"/"` would be `/clubs/`).

## Authorization

The API denies by default (ADR-0017): every route needs a signed-in User, and its 401 is added to the OpenAPI document for you.

- **A public route** says `.AllowAnonymous()` in the routing table and is added to `AnonymousOperations` in `OpenApiContractTests`, the reviewed list of what is public.
- **A route that needs a role** names a policy in the routing table.
- **A status the `Results<>` union doesn't describe** is declared with `.Produces(...)`, as `RedeemMagicLink` does for 401.

## Time

Take `TimeProvider` as a parameter and call `GetUtcNow()` (ADR-0016), so a test can set the clock.

## Testing

- Integration tests over HTTP against real Postgres: the seam for anything touching the database, with no in-memory or fake EF provider.
- `SocialTennis.Api.UnitTests` is for pure logic only: **if it needs a `DbContext`, it doesn't go there.** Validators and helpers like `Tokens` do.
- Validator tests use FluentValidation's `TestHelper` with the wire name: `ShouldHaveValidationErrorFor("email")`.
