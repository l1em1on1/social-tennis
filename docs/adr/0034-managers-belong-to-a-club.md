---
status: accepted
---

# Managers belong to a Club, and Leagues have their own managers

Manager was one role on the User, and whoever held it managed every Club (ADR-0003, ADR-0022). The owner (2026-11-24): that is wrong, a Manager belongs to a Club (#110). It had to change before a second Club could be created.

## Decision

- **A manager's role is a row, not a flag.** `ClubRoles` holds a Club, a User and a kind (`Manager`), one row per Club and User. The flag on the User is gone, so nothing can read it by mistake. Admin stays on the User, and an Admin manages every Club.
- **An Admin makes any User a manager of a Club, one or several** (`PUT`/`DELETE /clubs/{clubId}/managers/{userId}`, the Admin page's Roles sheet, one line per Club). A manager needn't play. The role stays until it is removed, whatever happens to their Players.
- **A Club's manager runs the Club and every League in it.** `Access/ManagerAccess` says so, from the database on every call, and the routing tables' policies and the handlers shared with Players all ask it (ADR-0017). A granted or removed role takes effect on the next request.
- **403 to someone who can see the thing, 404 to anyone else.** A member of the Club, a participant in the League, or anyone with a role there is told it isn't theirs to manage. An outsider is told nothing is there, as for anything else they may not know exists (ADR-0017). An outsider who used to get 403 now gets 404.
- **The web asks, it doesn't work it out.** `/auth/me` returns `admin` and `manages`; `GET /clubs` lists only the Clubs the caller has a role in (every Club for an Admin); a Game says whether the caller may manage it (`canManage`).
- **A Manager's add by email affects only their own Club.** The new Player doesn't join the login's other Clubs. A pre-approval another Club made for that email still stands: that was that Club's own decision.
- **A League can have its own managers (#220).** There are two things, held apart:
  - **The League manager role** (`ClubRoles`, kind `LeagueManager`) lets its holder create Leagues in that Club. Creating one gives them it. One row per Club and User: a manager outranks a League manager, and is made one by raising the row.
  - **An assignment** (`LeagueManagers`: a League, a User) lets that User run that one League, fully, whether or not they hold the role. It gives no say over the Club's members, or over who else runs the League.
  - A Club's managers and Admins need neither. **A League with no manager of its own is run by its Club's managers**, and says so.
- **A Club's managers and Admins grant both** (`PUT`/`DELETE /clubs/{clubId}/league-managers/{userId}` and `/leagues/{leagueId}/managers/{userId}`), **to anyone with a Player in that Club** (approved, not just asking), otherwise a 409 `no-player-in-club`. Either stays until it is taken, whatever happens to their Players. Taking the role keeps the assignments, which are taken on each League.
- **A League manager sees their own Leagues, and names.** Their list is the Leagues they run. Adding someone to a League shows every Club member's name, and emails only to a Club's manager (`GET /leagues/{leagueId}/addable-players`), so two members with one name can't be told apart by a League manager. They see email, gender and rating for the Players in their Leagues or asking to join.
- **Players see who runs their League, by name** (the ladder and a Game): the manager's Player in that Club, or "a manager" if they have none there. Never an email.

## Considered

- **Keep the flag, and add a list of Clubs beside it.** Two records of one thing that can disagree; the flag would go on being read by mistake.
- **A role claim per Club in the session.** Every request would load every role to use few, and the session scheme isn't where a rule about a route's ids belongs.
- **League managers as Club managers with fewer rights.** They would still be staff of the whole Club, and a League's own manager would need a flag on each handler. A separate table keeps "runs this League" one question.
- **Row-level security** (ADR-0003) still waits for a second real club. This scopes who may manage, not which rows a query sees.

## Consequences

- **A Manager of the old kind becomes a manager of every Club there was when the migration ran**, so nobody loses access (production had one Club). The migration can be reversed: whoever manages any Club gets the flag back.
- **Rolling back past this change** leaves managers without access, since the old code reads the flag the migration cleared. Reverse the migration, or have an Admin grant the roles again.
- **Creating a second Club is safe now.** ADR-0022 said not to until this was built.
- **The matrix of who may call what** (`ManagerAccessTests`) also has the League's manager, another League's manager and a League manager with no League, each of whom has left the Club since: an assignment or a role counts by itself.
- Every operation's callers are listed in `ManagerAccessTests`, now including the Club's manager, another Club's manager and a participant who has left the Club.
