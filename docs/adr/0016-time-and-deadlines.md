---
status: proposed
---

# Dates are calendar dates in the Club's time zone; instants are UTC; the clock is injected

A Stage has a StartDate and an EndDate, and "play by 30 November" means the end of that day where the club is, not midnight UTC. So League and Stage dates, and the Date a Game was played, are **calendar dates** (`DateOnly`, a Postgres `date`) with no time and no offset. A Club carries an IANA time zone (`Europe/London` for the first club), and a Stage's deadline passes at the end of its EndDate in that zone. Everything that records *when something happened* (a Score edit, a join time, a session expiry) is an instant stored in UTC (`DateTimeOffset`, `timestamptz`).

**The deadline triggers a close attempt; it locks nothing.** When a Stage's deadline passes, an auto-rolling League tries to close it. The Stage stays open while its Resolution Queue is non-empty, and a manually rolled Stage stays open until the Manager closes it. Scores are editable until the Stage *actually* closes (`CONTEXT.md`: Stage, ADR-0008), which can be well after EndDate. So the Score edit lock follows the Stage's closed state, never the date.

No code reads the system clock directly. Handlers and background services take `TimeProvider`, so a test can set the time to one second before and after a deadline. Whether a deadline has passed, and so when auto-roll attempts a close, is decided by the clock, and that is among the rules most worth testing (issue #1, Testing Decisions).

## Considered Options

- **Store the deadline as a UTC instant** — rejected: the Manager picks a day, not a moment. An instant derived from that day and the Club's zone is correct, but it is a second copy of the same fact, and it goes stale if the EndDate or the Club's zone is edited. The date and the zone stay the source of truth, and the instant is computed when it is needed.
- **Treat every date as UTC** — rejected: a UK club is on UTC+1 for half the year, so in summer a Stage's deadline would pass at 01:00 local time, an hour late, and the Date played on a Score entered just after local midnight would be recorded as the day before.
- **One app-wide time zone setting** — rejected: the zone belongs to the Club (ADR-0003), and putting it there costs one column.

## Consequences

The web app shows dates as the Club's calendar dates, never converted to the browser's zone. "Is this Stage past its deadline?" is one shared function of (EndDate, Club zone, now) and is not re-derived per handler. The auto-roll scheduler (#10) wakes on a timer and asks that function; it does not schedule a job for a stored instant.
