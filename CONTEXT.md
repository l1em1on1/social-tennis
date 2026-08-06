# Tennis League and Social Games

Domain glossary for the app that organizes and plans league and social tennis games.

## Language

**Club**:
A tennis club running leagues and socials. Owns Leagues, Socials, and Players via membership.

**User**:
The authentication identity — owns login/session. A User doesn't need a Player profile (e.g. a Manager-only account) and may hold more than one Player (e.g. a parent managing their children's Players).

**Player**:
A tennis domain profile — name, gender, level, Rating — participating in Clubs, Leagues, and Socials, used for ranking and league assignment. Held by a User (a User may hold several Players; a Player is held by exactly one User). A Player may belong to more than one Club (many-to-many), though in practice this is infrequent — typically one, rarely more than two or three.

**Player Rating**:
A Player's overall skill/ranking number. Renamed from the original "Score" field on Player to avoid colliding with the per-game Score entity below — they are unrelated concepts that happened to share a name.
_Avoid_: Score (on Player specifically — reserved for the per-game result)

**Game**:
Two sides playing a match on a Date at a Court — 1 Player per side for Singles, 2 per side otherwise (Doubles/Mixed/Foursomes). A Game belongs to either a Divisional League's Stage or a Social, never both — the same entity serves both contexts. Score is optional on a Game: League Games always record one (standings/Relegation depend on it), but Social Games often don't — some social events don't collect scores at all.

**Score**:
The recorded result of a single Game, when tracked: which Team's Player submitted it, ScoreA, ScoreB, Date, and the Reactions confirming or disputing it.

**Reaction**:
A confirmation or dispute from another player on a submitted Score — Approved (yes/no) plus an optional Comment.

**Team**:
A League-scoped pairing of 1 or 2 Players (depending on League Type) who compete together across a Stage. Predetermined by the manager, not re-formed per Game. Not used by Socials (a Social's Games are assembled directly from RSVP'd Players each time, no persistent Team) or by `Foursomes` Leagues (see League Type) — a Team requires a fixed partner across the Stage, which `Foursomes` doesn't have. A Team has no Rating of its own — it's always derived from its Players' Ratings (average, for now), never independently tracked.
_Avoid_: Pair (use Team even for singles, to keep one term across League Types)

**Competitor**:
The unit that occupies one of a Division's slots and accrues the result Relegation acts on. A Team for most League Types; an individual Player for `Foursomes`, which has no fixed Team (see League Type).

**Substitute**:
A Player standing in for a Team's regular Player in one specific League Game — one-off, doesn't change Team membership going forward. Only applies where Teams exist: League-only, and not for `Foursomes` (no fixed Team to substitute into) or Socials (no persistent Team at all).

**League Structure**:
How a League organizes competition, orthogonal to League Type (who plays): `Divisional` (Competitors sit in Divisions of ~4, round-robin, automatic per-Stage Relegation) or `Knockout` (single-elimination Bracket, no Divisions or Relegation). A League has both a Structure and a Type — e.g. a `DoublesMen` League can be either `Divisional` or `Knockout`.

**Division**:
A subset of a `Divisional` League's Competitors — ordinarily 4 — who play round-robin against each other within a Stage for standings purposes. Division membership can change via two distinct mechanisms: automatic end-of-Stage Relegation (every Stage, unconditionally), and ad-hoc movement if a Competitor drops out mid-League (exceptional, manual/automatic rebalancing). Not used by `Knockout` Leagues — see Bracket.
_Avoid_: Group (used in early feature notes, but the club's own vocabulary is Division)

**Bracket**:
A `Knockout` League's fixed, single-elimination set of rounds — lose once and the Competitor is out. Size is fixed once, when the League starts, based on how many Competitors signed up (rounded up to fit, e.g. to the next power of two) — it doesn't change afterward. Slots beyond the number of real Competitors are filled with a Bye. Seeded by Competitor Rating so strong Competitors are spread apart in early rounds — for Team-based Types this means the average of its Players' individual Ratings (see Team).

**Bye**:
A `Knockout` Bracket slot where a Competitor automatically advances to the next round without playing, used to make the numbers work when signups aren't an exact fit for the Bracket size.

**Stage**:
A time-boxed period (StartDate, EndDate) with a deadline for its Games to be completed. For `Divisional` Leagues, a Stage is a relegation period (in practice usually a month) at the end of which Relegation happens automatically: the Division's winning Competitor moves up, the losing Competitor moves down. For `Knockout` Leagues, a Stage is one Bracket round — split into several Stages so players have time to arrange each round's Games before the next round starts. Either way, a Stage's Games are played asynchronously — opposing Competitors arrange their own match date within the Stage's window — rather than all happening on one fixed date (contrast Social).

**Availability**:
A Player's set of free dates during a Stage, used to suggest a common date on which to arrange a Game against an opposing Competitor before the Stage's deadline. Today this coordination happens manually over WhatsApp; Availability replaces that. Distinct from RSVP — it's not a sign-up queue, just a scheduling aid between two already-determined Competitors.

**Relegation**:
`Divisional`-only: the automatic, end-of-every-Stage movement of the winning Competitor up and the losing Competitor down to the adjacent Division. Distinct from the ad-hoc Division movement triggered by a Competitor dropping out (see Division) — Relegation is routine, drop-out movement is exceptional. `Knockout` Leagues have no Relegation — a losing Competitor is simply eliminated from the Bracket.

**League Type**:
The player composition a League runs: `SinglesMen`, `SinglesWomen`, `SinglesMixed`, `DoublesMen`, `DoublesWomen`, `DoublesMix`, `Foursomes`. Independent of League Structure (see above). In `SinglesMen`/`SinglesWomen`/`SinglesMixed`/`DoublesMen`/`DoublesWomen`/`DoublesMix`, Competitors are fixed Teams. `Foursomes` is structurally different: within a `Divisional` League, a Division is 4 individual Players (no Team) who rotate doubles partners across the Stage — with 4 players, exactly 3 Games are needed for every player to partner with every other player once. Each Player's own Score is summed across their 3 Games to rank them within the Division; Relegation then promotes/demotes the top/bottom individual Player rather than a Team.

**Manager**:
A permissions role held by a User against a specific League — not a separate entity. A Manager will usually also hold a Player profile, but the role itself lives on the User account and doesn't require one.

**RSVP**:
A Player signalling they want to play in a specific Social. Ordered first-come-first-served; allocation into Games is capped by the number of courts the manager makes available for that date (4 Players per court). Renamed from the model's original "Vote" — no voting/choosing is involved, it's a sign-up queue.
_Avoid_: Vote
