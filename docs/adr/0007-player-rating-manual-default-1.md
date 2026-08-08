# Player Rating: manually entered, defaults to 1; derivation from results deferred

A Player's Rating is set and edited by hand by a Manager, and every new Player starts at Rating 1 — there is no unrated state. Nothing computes or updates Rating automatically in v1. The schema keeps Rating a first-class writable field so a calculation can later become another writer of it without restructuring.

Chosen because the club knows its players and a Manager-assigned number is good enough to seed Knockout Brackets and balance Social pairings, while a fair rating calculation is a real design problem (which results count, how much, how fast it moves) that shouldn't block v1.

## Considered Options

- **Compute Rating from League Game history** — the wanted future direction, explicitly deferred; not even partially built in v1.
- **Seed Rating from Level** — rejected: Level is the club's informal grouping (1–5), informational only; nothing computes from it.

## Consequences

Equal Ratings are common — an untouched signup seeds everyone at 1 — so every consumer of Rating (Bracket seeding, Social pairing) must break ties by a deterministic, documented rule, never a run-dependent one. Team Rating remains derived (average of members, per `CONTEXT.md`), never stored.
