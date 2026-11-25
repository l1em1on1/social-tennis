---
status: accepted
---

# Managers belong to a Club

Manager was one role on the User, and whoever held it managed every Club (ADR-0003, ADR-0022). The owner (2026-11-24): that is wrong, a Manager belongs to a Club (#110). It had to change before a second Club could be created.

## Decision

- **A manager's role is a row, not a flag.** `ClubRoles` holds a Club, a User and a kind (`Manager`), one row per Club and User. The flag on the User is gone, so nothing can read it by mistake. Admin stays on the User, and an Admin manages every Club.
- **An Admin makes any User a manager of a Club, one or several** (`PUT`/`DELETE /clubs/{clubId}/managers/{userId}`, the Admin page's Roles sheet, one line per Club). A manager needn't play. The role stays until it is removed, whatever happens to their Players.
- **A Club's manager runs the Club and every League in it.** `Access/ManagerAccess` says so, from the database on every call, and the routing tables' policies and the handlers shared with Players all ask it (ADR-0017). A granted or removed role takes effect on the next request.
- **403 to someone who can see the thing, 404 to anyone else.** A member of the Club, a participant in the League, or anyone with a role there is told it isn't theirs to manage. An outsider is told nothing is there, as for anything else they may not know exists (ADR-0017). An outsider who used to get 403 now gets 404.
- **The web asks, it doesn't work it out.** `/auth/me` returns `admin` and `manages`; `GET /clubs` lists only the Clubs the caller has a role in (every Club for an Admin); a Game says whether the caller may manage it (`canManage`).
- **A Manager's add by email affects only their own Club.** The new Player doesn't join the login's other Clubs. A pre-approval another Club made for that email still stands: that was that Club's own decision.
- **League managers (#220)** were decided in the same conversation: a second kind of role per Club, and managers of single Leagues. Not built yet; this ADR is amended when they are.

## Considered

- **Keep the flag, and add a list of Clubs beside it.** Two records of one thing that can disagree; the flag would go on being read by mistake.
- **A role claim per Club in the session.** Every request would load every role to use few, and the session scheme isn't where a rule about a route's ids belongs.
- **Row-level security** (ADR-0003) still waits for a second real club. This scopes who may manage, not which rows a query sees.

## Consequences

- **A Manager of the old kind becomes a manager of every Club there was when the migration ran**, so nobody loses access (production had one Club). The migration can be reversed: whoever manages any Club gets the flag back.
- **Rolling back past this change** leaves managers without access, since the old code reads the flag the migration cleared. Reverse the migration, or have an Admin grant the roles again.
- **Creating a second Club is safe now.** ADR-0022 said not to until this was built.
- Every operation's callers are listed in `ManagerAccessTests`, now including the Club's manager, another Club's manager and a participant who has left the Club.
