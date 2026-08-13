# List endpoints return an envelope, not a bare array

A list endpoint returns an object with a domain-named collection and a `page` block — `{ "clubs": [...], "page": { "total": 1 } }` — never the array itself. A bare array has nowhere to put list-level facts, so adding the first one is a breaking change for every client. Paging is coming, and eight-plus list endpoints are queued behind `GET /clubs` (#4, #5, #7, #12, #15, #17, #18 …); the shape that lands first is the shape they all copy.

## The rules

**Envelopes are bespoke per endpoint, with a domain-named collection.** `GetClubsResponse` holds `Clubs`, not `Items`. A single generic `ListResponse<T>` would be less code, but no endpoint could then carry its own list-level stat — and those are coming: a leagues list wants an open-for-joining count, standings want a last-updated timestamp. A shared type absorbing all of them becomes a bag of nullable fields that are meaningless on most endpoints. .NET would also render the generic as `ListResponseOfClubSummary` in the schema, which is what the TypeScript client would show.

**The envelope is named for its endpoint.** `GetClubsResponse`, per the naming rule in `docs/agents/api-endpoints.md`. This *removes* the "collections name the element" carve-out that issue #29 introduced into [`docs/agents/api-endpoints.md`](../agents/api-endpoints.md) — the envelope is named for its endpoint like every other contract, and its element is named for its resource. One rule, one carve-out, instead of one rule and two.

**The list element is an independent record, not shared with any single-item response.** `ClubSummary` today; a future `GET /clubs/{id}` gets its own `ClubDetails` rather than reusing it. The two answer different questions and are expected to diverge — the point is payload independence, so that enriching a detail view (nested Leagues, a computed member count) never silently widens every row of a fifty-item list. The duplication is the feature, not debt.

**`page` is a shared `PageInfo`.** The one genuinely uniform part of an otherwise bespoke envelope, so it lives in the API's top-level `Contracts/` folder — the first contract that isn't feature-scoped, following the same "cross-cutting things get a top-level folder" rule as `Validation/` and `Authentication/`. `Limit` and `Offset` join it when paging lands and every list gets them at once.

**`Total` means the number of items matching the query, ignoring any paging window.** Today that always equals the returned collection's length, which makes it look redundant. It isn't: fixing the meaning now means paging later changes the *value* but never the *definition*. Ship it as "how many I returned" and the day paging arrives every client reading it as a length breaks silently against an unchanged field name.

**`page` is mandatory on every list envelope**, including lists that can never grow — the four Players in a Division, the Competitors in one Bracket round. A rule containing a judgement call ("include it when the list might get long") is decided differently by each ticket; a redundant integer costs less than an inconsistent envelope across twelve endpoints.

## Considered Options

- **Bare array** — the status quo, and the cheapest thing that works until the first moment it doesn't. Rejected: every list-level addition is then a breaking change, and there is no version of this API where we never want a count.
- **Generic `ListResponse<T>`** — one type, uniform everywhere, impossible to get wrong. Rejected for the per-endpoint stats above, and because the generated schema name leaks the C# generic into TypeScript.
- **Abstract `ListResult<T>` base record** — rejected on two counts. It forces `items` onto the wire in place of a domain-named collection, and C# positional records do not inherit for free: `record GetClubsResponse(IReadOnlyList<ClubSummary> Items, PageInfo Page) : ListResult<ClubSummary>(Items, Page)` re-declares every parameter, so it *adds* duplication.
- **`IListResult<T>` / `IPaged` interfaces** — rejected as an enforcement mechanism, though harmless. Interfaces are invisible to both System.Text.Json and the OpenAPI generator, so they have no wire effect at all; and crucially, neither an interface nor a base class can compel a *future* endpoint to use an envelope. Nothing stops the next handler returning `List<T>` directly.

The last two rejections rest on a fact worth recording, because it is not obvious and was verified rather than assumed: **C# inheritance between contracts does not reach the wire.** A derived record is emitted as a flat schema with every inherited property inlined, and the base type does not appear in `components.schemas` at all. ASP.NET Core composes schemas (`allOf`, discriminators) only for hierarchies declared with `[JsonPolymorphic]`/`[JsonDerivedType]`. Any "share the shape via a base record" approach therefore buys nothing that the client can see.

## Consequences

The convention is enforced by `OpenApiContractTests.No_endpoint_returns_a_bare_array`, which walks the generated document and fails on any 2xx JSON response whose schema is a top-level array. This asserts the wire contract rather than the C# types, so it holds however the violation is written — and it is the *only* thing enforcing the rule, since no type-level mechanism can.

That is a weaker guarantee than this repo usually accepts. `ValidatesBody<,>` makes the filter and its OpenAPI metadata inseparable; the `new()` constraint makes "validators never touch the database" a compile error; `SocialTennis.Api.UnitTests` references no test host, so a database test cannot live there by accident. Each of those is a compile-time fact. This one is a test — it fails in CI rather than in the editor, and it can be deleted. That was accepted deliberately: the alternatives that *are* structural (base record, generic) each cost something the wire shape shouldn't pay.

`web/` reads the collection off the envelope rather than treating the response as the array, and every future list endpoint's client code does the same.
