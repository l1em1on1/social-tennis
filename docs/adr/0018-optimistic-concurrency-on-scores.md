---
status: proposed
---

# Score edits use optimistic concurrency; a stale edit is rejected, not merged

In the first release any Player who played a Game can edit its Score, and so can a Manager (ADR-0008, Phasing). Two of them can open the same Score and save different corrections. With last-write-wins the second save overwrites the first, and the edit history then shows a change "from" a value its author never saw. That is the quiet rewrite the history exists to prevent.

A Score carries a version. Reading a Score returns it, an edit sends back the version it was based on, and the API rejects an edit whose version is stale with **409** (ADR-0017) and the current Score in the response. The web app shows the current Score and asks the Player to redo the change. The version is Postgres's own row version: a `uint` property configured with `IsRowVersion()`, which the Npgsql provider maps to the `xmin` system column, so there is no column to add or maintain.

## Considered Options

- **Last write wins** — rejected for the reason above.
- **Merge the two edits** — rejected: a Score is a few numbers that only make sense together.
- **Pessimistic lock while a Player has the form open** — rejected: phones lose signal and lock screens, and an abandoned lock needs a timeout and an unlock path.

## Consequences

The same token is the default for any other record that two people edit (a League's settings, the proposed starting ladder), adopted per ticket when the collision is real. Append-only records (edit history, join requests) don't need it.
