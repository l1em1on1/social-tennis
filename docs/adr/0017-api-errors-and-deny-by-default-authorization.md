---
status: proposed
---

# The API denies by default, and business-rule failures are ProblemDetails with a fixed status vocabulary

**Authorization is deny-by-default.** A fallback policy requires a signed-in User on every endpoint that declares nothing else; the few anonymous routes (requesting and redeeming a magic link, `/health`, the OpenAPI document) opt out with `AllowAnonymous()`. Roles are named policies (`Manager`, `Admin`) attached in the feature's routing table (ADR-0010), never an `if` inside a handler. Before this, an endpoint was public unless someone remembered `RequireAuthorization()`, and `GET /clubs` was. This repo prefers rules the compiler or the framework enforces over rules a reviewer has to remember (ADR-0011, ADR-0012); the fallback policy is that for authorization, per the ASP.NET Core docs ("secure by default").

**Every error has a ProblemDetails body**, and the status says what kind of failure it is:

| Status | Meaning | Example |
| --- | --- | --- |
| 400 | The request is malformed: shape validation (ADR-0011) | A Set with negative games |
| 401 | No valid session | Expired session token |
| 403 | Signed in, but not allowed | A Player calling a Manager endpoint; editing another User's Player |
| 404 | No such resource, or one the caller may not know exists | A League at a Club the Player isn't in |
| 409 | Well-formed, but the current state forbids it | Starting a League twice; closing a Stage with a non-empty queue; a stale Score edit (ADR-0018) |
| 429 | Rate limited | Repeated magic-link requests |

A 409 carries a stable machine-readable `type` (e.g. `stage-queue-not-empty`), so the web app can show a specific message without matching on English text. Every status a handler can return is in its `Results<>` union or declared in the routing table, so it reaches the generated client. A contract test asserts that every operation outside an explicit anonymous allow-list declares 401.

## Considered Options

- **422 for business-rule failures** — rejected: 400 already means "the request alone is wrong" and 409 "the request is fine but the state isn't". A third code for the gap between them is a judgement call made differently in each ticket.
- **Opt-in authorization, enforced by a test** — rejected as the primary mechanism: a test that lists protected endpoints can be forgotten. The contract test is kept as a second line, since it also keeps the 401 in the client.

## Consequences

Integration tests sign in before calling anything but the anonymous routes. Rules validated against the database (Score format against the League, #38) answer 400 with the same `errors` dictionary shape as the validation filter, so a form handles both the same way.
