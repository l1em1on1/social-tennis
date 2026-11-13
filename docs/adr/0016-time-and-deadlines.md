---
status: proposed
---

# Dates are calendar dates in the Club's time zone; instants are UTC; the clock is injected

A Stage has a StartDate and an EndDate, and "play by 30 November" means the end of that day where the club is, not midnight UTC. So League and Stage dates, and the Date a Game was played, are **calendar dates** (`DateOnly`, a Postgres `date`) with no time and no offset. A Club carries an IANA time zone (`Europe/London` for the first club), and a Stage's deadline passes at the end of its EndDate in that zone. Everything that records *when something happened* (a Score edit, a join time, a session expiry) is an instant stored in UTC (`DateTimeOffset`, `timestamptz`).

No code reads the system clock directly. Handlers and background services take `TimeProvider`, so a test can set the time to one second before and after a deadline. Stage close, auto-roll and the Score edit lock are all decided by the clock, and they are the rules most worth testing (issue #1, Testing Decisions).

## Considered Options

- **Store the deadline as a UTC instant** — rejected: the Manager picks a day, not a moment, and a stored instant drifts by an hour across a daylight-saving change.
- **Treat every date as UTC** — rejected: a UK club is on UTC+1 for half the year, so a Stage would close at 01:00 local time and a Score entered late on the last evening would be locked out on the wrong day.
- **One app-wide time zone setting** — rejected: the zone belongs to the Club (ADR-0003), and putting it there costs one column.

## Consequences

The web app shows dates as the Club's calendar dates, never converted to the browser's zone. "Is this Stage past its deadline?" is one shared function of (EndDate, Club zone, now) and is not re-derived per handler. The auto-roll scheduler (#10) wakes on a timer and asks that function; it does not schedule a job for a stored instant.
