# League Scores settle by player vote; Manager only on deadlock; Socials exempt

A submitted League Game Score opens a voting window for every Player who played the Game (both sides; a Substitute votes in place of the Player they replaced, and the voter set is fixed at submission because substitution closes then). Players approve, or dispute with an optional Comment and optionally a proposed corrected Score. **Silence is an abstention, not a vote**: with no votes cast the submitted Score stands; otherwise the proposal with the majority of cast votes wins — so a lone uncontested counter-proposal wins. A tie is a Deadlock, broken by a Player changing their vote or escalated to the Manager. The window closes a configurable number of days after submission or at Stage close, whichever is sooner, so results settle continuously.

At Stage close, whatever remains unsettled — Deadlocked Scores and Games with no Score at all — lands in a Manager resolution queue, and the Stage cannot close (so Relegation cannot run, and a Knockout round cannot advance) until the queue is empty. This deliberately trades liveness for correctness: standings are never computed over disputed results, at the cost of an absent Manager stalling the League — which is why the blocked state must be loudly visible to Manager and Players alike.

Social Game Scores are exempt: recorded and settled immediately by the submitter, correctable, no protocol — nothing rides on a Social result, so agreement machinery would be pure overhead.

Chosen because the club's alternative is arguing over WhatsApp with the Manager as referee of first resort; this makes the Manager the referee of *last* resort while keeping obstacles minimal (the same silence-is-consent shape is reused for Substitute proposals).

## Considered Options

- **Single-opponent confirmation** (the original Reaction model: opposing side approves yes/no) — rejected: gives one player a veto, no path to a corrected value, and every disagreement lands on the Manager immediately.
- **Relegation runs on time over unsettled Scores, corrected retroactively** — rejected: retroactive Division moves are worse than a delayed Stage.
- **The same protocol for Social Games** — rejected as overhead; see above.

## Consequences

Score needs proposal/vote/deadlock state and a settlement clock; the Manager needs a resolution queue surfaced prominently; standings must mark pending results as pending rather than dropping them silently. The N-day window is club-configurable.
