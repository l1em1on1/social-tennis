# API endpoints are one static class per endpoint

Minimal API endpoints are declared as **one static class per endpoint**, in a feature folder, each holding a single `HandleAsync`. **Routing is separate from handling**: every route, verb, filter, and authorization policy for a feature is declared in that feature's `<Feature>Endpoints.cs`, which acts as the feature's routing table. Request and response contracts live in the feature's `Contracts.cs`.

The starting point was four auth endpoints as inline lambdas inside one `MapAuthEndpoints` extension, plus a fifth lambda in `Program.cs`. Lambdas gave the handlers no name to navigate to, no signature to call from a test, and no obvious place to stop growing. The unit of organisation is now a file: adding an endpoint means adding one file and one line in the routing table.

Keeping the route out of the endpoint class is what makes the routing table worth having. An endpoint class that declared its own `Map` would scatter the HTTP surface across as many files as there are endpoints, so answering "what does `/auth` expose, and which parts of it require authorization?" would mean opening all of them. Handlers are passed as method groups, so the wiring is still compiler-checked; the cost is that a route and its handler are no longer adjacent, which is the trade made deliberately.

Handlers hold their own logic and take their dependencies as handler parameters, bound by ASP.NET. There is no service layer between endpoint and `DbContext` — the endpoint class already provides the named, individually testable unit a service would have provided, and a service with exactly one caller is indirection without a payer. A service gets extracted when a second caller appears; OAuth login (ADR-0004) sharing user-provisioning with magic-link is the expected first case.

Registration is explicit rather than assembly-scanned. The endpoint classes are plain statics with no marker interface, so a scan could only match on method name and signature — a renamed or mistyped handler would silently fail to register and surface as a 404 at runtime. The routing table passes each handler as a method group, so the compiler checks it.

## Considered Options

- **Named static methods referenced as method groups**, all in one per-feature class — the smallest change, but the per-feature file keeps growing and contracts still pile up at the top of it.
- **Class per endpoint with the route declared inside it** — the usual REPR shape. Rejected: it hides the feature's HTTP surface across N files.
- **Class per endpoint, routes hoisted into a per-feature routing table** — chosen. One file per endpoint, greppable from the generated TS client's operation name straight to the C# source, with the routes readable in one place.
- **MediatR** — rejected. Class-per-endpoint *is* the handler-isolation pattern, so a mediator on top makes every operation two classes and two contract pairs for the same isolation, bought twice. It also breaks the `TypedResults` union → OpenAPI inference that generates the TS client (ADR-0001), replacing it with hand-written status mapping; `Send(command)` has no compile-time link to its handler, so go-to-definition lands on an interface; and MediatR is now dual-licensed by Lucky Penny Software, requiring a paid key above a revenue threshold. If pipeline behaviours are ever genuinely needed, `IEndpointFilter` on a `MapGroup` is the native equivalent.
- **MVC controllers** — rejected; discards the minimal-API basis of the contract pipeline.

## Validation

Shape validation runs in an endpoint filter, not in the handler. A request contract implements `IValidatable`, and a single generic `ValidationFilter<T>` short-circuits with `TypedResults.ValidationProblem`. Rules stay readable C# on the record rather than attribute metadata.

.NET 10's built-in `builder.Services.AddValidation()` does the same job with DataAnnotations and was considered; we own the filter instead to keep the rules as ordinary code that can be read and unit-tested directly. The cost is that we maintain roughly twenty lines the framework would have supplied.

Moving validation out of the handler takes `ValidationProblem` out of the `Results<>` union, so the 400 is no longer inferred into the OpenAPI document. `ValidatesBody<T>()` therefore attaches the filter *and* `ProducesValidationProblem` in one inseparable call — attaching only the filter would leave the endpoint working while the generated client silently lost the response.

## Consequences

`api/src/SocialTennis.Api/` gains `Features/<Feature>/`, `Validation/`, and `Authentication/` — the last being the former `Auth/` folder, renamed so that a feature namespace of `...Features.Auth` doesn't shadow it and force full qualification. `Program.cs` maps no endpoints itself. The recipe for adding an endpoint is `docs/agents/api-endpoints.md`.

The project's testing seam narrows rather than reverses. Integration-over-HTTP-against-real-Postgres remains the default and the only way anything touching the database is tested; no in-memory or fake provider enters the repo. Because `HandleAsync` and `Validate()` are now directly callable, pure logic with no `DbContext` may instead be unit-tested in `SocialTennis.Api.UnitTests`, which references no test host by design. "Needs a `DbContext`" is the whole of the rule.

`GET /clubs` still serialises the `Club` entity rather than a response contract — the one place the wire format is coupled to the EF model. It was left unchanged here so this restructure could be verified by unchanged integration tests and a byte-identical generated client; the DTO is issue #28.
