# Request validation uses FluentValidation, declared inside the request contract

Shape validation is expressed with **FluentValidation**. Each request contract nests its own `public sealed class Validator : AbstractValidator<TRequest>`, and the routing table names both types: `.ValidatesBody<MagicLinkRequest, MagicLinkRequest.Validator>()`. This supersedes the hand-rolled `IValidatable` interface introduced in ADR-0010; the endpoint filter and the `ValidatesBody` extension survive unchanged in purpose.

ADR-0010 chose to own roughly twenty lines rather than take a dependency, on the grounds that rules should be ordinary readable C# rather than attribute metadata. That reasoning holds — it just doesn't require writing the library. `IValidatable` gave every contract one method returning `Dictionary<string, string[]>?`, so each contract hand-assembled its own error dictionary and invented its own convention for keys and messages. At one contract that is fine. The rule vocabulary (`NotEmpty`, `MaximumLength`, cascade behaviour), rule composition, and per-property error aggregation are exactly the things a second and third contract would otherwise grow by hand, each slightly differently.

## Considered Options

- **Keep `IValidatable`** — no dependency, but every contract re-implements error aggregation.
- **.NET 10's built-in `AddValidation()`** — rejected again, and for a sharper reason than in ADR-0010. It is DataAnnotations-driven and applies its filter by source-generated discovery, so a contract is validated because a generator found it: nothing at the route says so, and a validator that stops being discovered **fails open**, accepting bad input. That is worse than the silent-404 failure mode that made ADR-0010 reject assembly scanning for handlers. The official docs also do not show it contributing the 400 to the OpenAPI document, so the desync `ValidatesBody` exists to prevent would remain.
- **FluentValidation bridged into `AddValidation()`** via a custom `IValidatableInfoResolver` — rejected. It appears in neither the ASP.NET Core documentation nor the v10.0.1 public API listings under direct query, so the adapter would be bespoke code against an undocumented seam: strictly more code owned than the filter it replaces, which is the opposite of the motivation.
- **SharpGrip.FluentValidation.AutoValidation** — rejected. It validates every endpoint automatically but declares nothing, so each validated endpoint would silently lose its 400 from the generated TS client.
- **FluentValidation behind the existing filter** — chosen.

## The validator is nested, and constructed rather than injected

The validator lives *inside* the record it validates. The rules cannot drift into a file nobody opens, and `MagicLinkRequest.Validator` is a compile-time handle rather than a naming convention.

`ValidatesBody<TRequest, TValidator>` constrains `TValidator : AbstractValidator<TRequest>, new()` and `ValidationFilter` news it up into a `static readonly` field — no DI registration, and `Program.cs` is untouched. Three consequences, all deliberate:

- A contract marked for validation with no matching validator is a **compile error**. With DI it would be a runtime 500 on first request. This is ADR-0010's argument for compiler-checked method groups over assembly scanning, applied to the same kind of wiring.
- The `new()` constraint means a validator **cannot take dependencies at all**. "Validation never touches the database" stops being a documented rule and becomes one the compiler enforces; business rules needing a `DbContext`, configuration, or the current user stay in `HandleAsync`.
- `FluentValidation.DependencyInjectionExtensions` is not referenced. The core package is the whole dependency.

The cost is a call site naming two types where one would do, and a validator that can never be parameterised. Both were accepted knowingly.

## Error keys are wire names, not CLR names

FluentValidation names errors after the CLR property, which would put `"Email"` in `HttpValidationProblemDetails.errors` where the API previously returned `"email"`. That dictionary is free-form in the generated TS client, so the TS compiler cannot see such a change — it would surface only as a form field that quietly stops highlighting.

`.OverridePropertyName("email")` on the rule pins it locally, next to the rule, identical in tests and production. The alternative, `ValidatorOptions.Global.PropertyNameResolver`, was rejected: it is static global state set in `Program.cs`, which unit tests do not execute, so tests would assert one casing while production returned another.

The knock-on is that `TestHelper`'s expression overload (`ShouldHaveValidationErrorFor(r => r.Email)`) resolves to the CLR name and does not match. Tests therefore assert the **wire** field name, `ShouldHaveValidationErrorFor("email")` — which is the more honest assertion anyway, since the wire name is now deliberately independent of the property name.

## Consequences

`Validation/IValidatable.cs` is deleted; `ValidationFilter` and `EndpointValidationExtensions` gain a second type parameter. `MagicLinkRequest` is the only contract converted — `RedeemMagicLinkRequest` (named `RedeemRequest` until #29) stays unvalidated, so an empty token still returns **401** rather than 400, which is the right answer for a credential endpoint and keeps this change free of wire-contract movement.

Status codes and schemas are unchanged: `docker compose run --rm web npm run api:generate` reproduces `web/src/lib/api/schema.d.ts` with identical content. The one visible behaviour change is that the single message `"A valid email address is required."` becomes two — an absent address and a malformed one now read differently. The recipe is `docs/agents/api-endpoints.md`.
