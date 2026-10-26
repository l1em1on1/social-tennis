# Tennis League and Social Games

Domain glossary for the app that organizes and plans league and social tennis games.

## Language

**Club**:
A tennis club running leagues and socials. Owns Leagues, Socials, and Players via membership.

**League**:
A Club competition combining a League Type (who plays) with a League Structure (how competition is organized), run by a Manager over a set duration as a sequence of Stages.

**Social**:
A one-off Club event on a fixed Date where RSVP'd Players are allocated into Games — informal, no standings or Relegation. Unlike a Stage's asynchronously arranged Games, a Social's Games all happen on its single Date.

**User**:
The authentication identity — owns login/session. A User doesn't need a Player profile (e.g. a Manager-only account) and may hold more than one Player (e.g. a parent managing their children's Players).

**Player**:
A tennis domain profile — name, gender, Level, Rating — participating in Clubs, Leagues, and Socials, used for ranking and league assignment. A Manager may create a Player with an email before its holder has ever logged in; the User who logs in with that email claims it — every unclaimed Player carrying that email. Level (1–5) is the club's informal grouping, informational only — nothing computes from it. An unclaimed Player has no holding User; otherwise a Player is held by exactly one User, and a User may hold several Players. A Player may belong to more than one Club (many-to-many), though in practice this is infrequent — typically one, rarely more than two or three.

**Player Rating**:
A Player's overall skill/ranking number, entered and edited manually by a Manager — nothing computes it in v1 (deriving it from League Game history is a wanted future direction, deliberately deferred — ADR-0007). Defaults to 1 for a new Player, so no Player is ever unrated; consumers of Rating (Bracket seeding, Social pairing) break the resulting ties deterministically. Renamed from the original "Score" field on Player to avoid colliding with the per-game Score entity below — they are unrelated concepts that happened to share a name.
_Avoid_: Score (on Player specifically — reserved for the per-game result)

**Game**:
Two sides playing a match on a Date — 1 Player per side for Singles, 2 per side otherwise (Doubles/Mixed/Foursomes). Court is an optional free-text label, not an entity (ADR-0006): on League Games the players arrange their court outside the app and may note it (e.g. "6 - astro"); on Social Games the Manager sets it so players know where to go. A Game belongs to either a League Stage (Divisional or Knockout) or a Social, never both — the same entity serves both contexts. Score is optional on a Game: League Games always record one (standings/Relegation depend on it), but Social Games often don't — some social events don't collect scores at all. A Game is either **played** or **unplayed**, and the distinction survives resolution: a result awarded by Walkover is not a played result.

**Score**:
The recorded result of a single Game, when tracked: its Sets, the Date played, which Player submitted it, every later edit (who, when, from what to what), and — if the Game ended early — a Retirement. A League Game's Score is editable by any Player who played the Game until its Stage closes, and a Manager can override it; the agreement protocol (see Reaction) is the target model, phased in later (ADR-0008). A Social Game's Score is final on submission, no protocol.

**Set**:
One set within a Game's Score: the games each side won, plus the tiebreak points when the set went to a tiebreak. Each League fixes its games per Set (usually 6 or 9, with the tiebreak played at one game short of that apiece — 5–5 or 8–8) and its Sets per Game (one, or best of three).

**Match Tiebreak**:
A deciding Set played as a single first-to-10 tiebreak instead of a full Set, in a best-of-three League that allows it. Counts as one game wherever games are summed (games banked, games difference).

**Retirement**:
A Game that ended early — injury, rain, court time running out — recorded with the Sets as they stood and an explicitly declared winner. A played result, unlike a Walkover.

**Reaction**:
A vote cast on a submitted League Game Score by a Player who played that Game: approve, or dispute with an optional Comment and optionally a proposed corrected Score. Settlement rules (silence is abstention, majority of cast votes wins, ties Deadlock to the Manager's resolution queue) are ADR-0008; Social Games have no Reactions.

**Deadlock**:
A tied Reaction vote on a Score. Broken by a Player changing their vote, or resolved by the Manager — whose resolution queue gates Stage close (ADR-0008).

**Walkover**:
A Manager resolution of an unplayed Game awarding the result to one side without play — used at Stage close or on a Withdrawal.

**Void**:
A Manager resolution cancelling an unplayed Game entirely — it counts for nothing in standings.

**Team**:
A League-scoped pairing of 1 or 2 Players (depending on League Type) who compete together for the life of the League — changing partner means a Withdrawal plus a fresh join, never an in-place re-pair. Predetermined by the manager, not re-formed per Game. Not used by Socials (a Social's Games are assembled directly from RSVP'd Players each time, no persistent Team) or by `Foursomes` Leagues (see League Type) — a Team requires a fixed partner, which `Foursomes` doesn't have. A Team has no Rating of its own — it's always derived from its Players' Ratings (average, for now), never independently tracked.
_Avoid_: Pair (use Team even for singles, to keep one term across League Types)

**Competitor**:
The unit that occupies one of a Division's slots and accrues the result Relegation acts on. A Team for most League Types; an individual Player for `Foursomes`, which has no fixed Team (see League Type).

**Substitute**:
A Player standing in for a Team's regular Player in one specific League Game — one-off, doesn't change Team membership going forward. Singles Teams included: the Substitute plays that Game as the whole side, and the result still counts to the regular Competitor. Any Player of the same Club is eligible. Nominating one creates a proposal the opposing side may reject within a configurable window; silence is acceptance. Substitution closes once a Score is submitted for the Game. Only applies where Teams exist: League-only, and not for `Foursomes` (no fixed Team to substitute into) or Socials (no persistent Team at all).

**League Structure**:
How a League organizes competition, orthogonal to League Type (who plays): `Divisional` (a ladder of fixed-size Divisions, per-Stage Relegation — ADR-0013) or `Knockout` (single-elimination Bracket, no Divisions or Relegation). A League has both a Structure and a Type — e.g. a `DoublesMen` League can be either `Divisional` or `Knockout`.

**Division**:
One rung of a `Divisional` League's ladder: a fixed-size set of Competitors who play each other within a Stage — 2 Competitors for Singles and Doubles Types (one Game per Stage), 4 individual Players for `Foursomes` (three rotation Games per Stage). Divisions are numbered from the top, Division 1 being the highest. Membership changes via two distinct mechanisms: end-of-Stage Relegation (routine) and Manager moves (exceptional — e.g. a Withdrawal). Not used by `Knockout` Leagues — see Bracket (ADR-0013).
_Avoid_: Group (used in early feature notes, but the club's own vocabulary is Division)

**Bracket**:
A `Knockout` League's fixed, single-elimination set of rounds — lose once and the Competitor is out. Size is fixed once, when the League starts, based on how many Competitors signed up (rounded up to fit, e.g. to the next power of two) — it doesn't change afterward. Slots beyond the number of real Competitors are filled with a Bye. Seeded by Competitor Rating so strong Competitors are spread apart in early rounds — for Team-based Types this means the average of its Players' individual Ratings (see Team).

**Bye**:
A slot that makes the numbers work when Competitors don't fit the structure exactly. In a `Knockout` Bracket, the Competitor drawing it advances to the next round without playing. In a `Divisional` ladder, it is the one Competitor left over when the bottom Division can't be filled: they sit out that Stage without playing and without moving.

**Stage**:
A time-boxed period (StartDate, EndDate) with a deadline for its Games to be completed. Every League configures a Stage length at creation, whatever its Structure — a Stage is always the timeframe Players get to arrange and play their Games. For `Divisional` Leagues, a Stage is a relegation period (in practice usually a month) ending in Relegation. A League either auto-rolls — reaching EndDate triggers a close attempt, but the Stage stays open until nothing awaits the Manager's resolution; it then closes, Relegation runs, and the next Stage opens — or rolls manually, in which case the Stage stays open until the Manager closes it, likewise subject to an empty resolution queue. Scores remain editable by participating Players until the Stage actually closes. For `Knockout` Leagues, a Stage is one Bracket round — split into several Stages so players have time to arrange each round's Games before the next round starts. Either way, a Stage's Games are played asynchronously — opposing Competitors arrange their own match date within the Stage's window — rather than all happening on one fixed date (contrast Social).

**Availability**:
A Player's set of free dates during a Stage, used to suggest a common date on which to arrange a Game against an opposing Competitor before the Stage's deadline. Today this coordination happens manually over WhatsApp; Availability replaces that. Distinct from RSVP — it's not a sign-up queue, just a scheduling aid between two already-determined Competitors.

**Withdrawal**:
A Competitor leaving a League mid-season. The Player files a request in-app; nothing changes until the Manager accepts it. On acceptance the Competitor is marked Withdrawn, their unplayed fixtures go to the Manager resolution queue to be settled as walkovers or voids, and the Manager may rebalance Divisions with the mid-League movement tools. Settled results are never rewritten.

**Relegation**:
`Divisional`-only: the end-of-every-Stage movement, by Standings, of each Division's top Competitor up and bottom Competitor down to the adjacent Division. The top Division's winner and the bottom Division's loser stay put; in `Foursomes` the middle two Players stay too. Distinct from the ad-hoc Division movement triggered by a Competitor dropping out (see Division) — Relegation is routine, drop-out movement is exceptional. `Knockout` Leagues have no Relegation — a losing Competitor is simply eliminated from the Bracket.

**Standings**:
A Division's ranking within a Stage, computed from its Games' results: matches won, then games difference, then head-to-head, then the Manager's decision. For `Foursomes`: games banked, then head-to-head, then the Manager's decision. An unresolved tie holds the Stage's close like any other unresolved result.

**League Type**:
The player composition a League runs: `SinglesMen`, `SinglesWomen`, `SinglesMixed`, `DoublesMen`, `DoublesWomen`, `DoublesMix`, `Foursomes`. Independent of League Structure (see above), with one exception: `Foursomes` is `Divisional`-only — its Division-of-4 partner rotation has no meaning in a single-elimination Bracket, so a `Foursomes` `Knockout` League is invalid. In `SinglesMen`/`SinglesWomen`/`SinglesMixed`/`DoublesMen`/`DoublesWomen`/`DoublesMix`, Competitors are fixed Teams. `Foursomes` is structurally different: within a `Divisional` League, a Division is 4 individual Players (no Team) who rotate doubles partners across the Stage — with 4 players, exactly 3 Games are needed for every player to partner with every other player once. Each Player banks the games their side won in each of their 3 Games, and the summed total ranks them within the Division; Relegation then promotes/demotes the top/bottom individual Player rather than a Team.

**Admin**:
The top permissions role, held by a User — not a separate entity. An Admin creates Clubs and grants/revokes roles (Manager, Admin). The first Admin is seeded at deployment so the system is never admin-less. Effectively global in v1's single-Club world; per-Club scoping is deferred (ADR-0003).

**Manager**:
A permissions role held by a User, granted by an Admin — not a separate entity. A Manager creates and runs Leagues and Socials for the Club. Effectively club-wide in v1; per-Club/per-League scoping is deferred (ADR-0003). A Manager will usually also hold a Player profile, but the role itself lives on the User account and doesn't require one.

**RSVP**:
A Player signalling they want to play in a specific Social. Ordered first-come-first-served; allocation into Games is capped by the number of courts the manager makes available for that date (4 Players per court). Renamed from the model's original "Vote" — no voting/choosing is involved, it's a sign-up queue.
_Avoid_: Vote
