---
status: accepted
---

# A League ranks its Standings Standard, or by Bonus points

Standings ranked one way (#10): matches won, then games difference, then head-to-head, then the Manager. The owner (2026-11-25 and 2026-11-26, #214) wants a second way that rewards turning up: "bonus for played games and all games played, otherwise standard". Foursomes is different, and is #14's.

## Decision

- **A Standings mode, set when the League is created and fixed afterwards,** like its Score format. Only a Divisional League of Teams (Singles and Doubles) has one; it defaults to Standard. Knockout ranks nothing and Foursomes has its own rule, so both have none, and the API refuses one (400, `standingsMode`).
- **Standard, as before:** matches won, then games difference, then head-to-head, then the Manager.
- **Bonus points:** 1 point for a match won, 1 for a match played, and 1 more for playing every one of the Team's Games in its Division that Stage. Then games difference, head-to-head and the Manager, as Standard.
- **Played means the Game has a Score.** A Retirement counts as played for both Sides and as won for its winner. A Game not played, a Void or (once built) a Walkover, gives neither Side anything and keeps both from the bonus point.
- **Head-to-head is the Game the tied Teams already played.** No extra Game is played to settle a tie.
- **One ranking, two keys.** `Standings.Rank` takes the mode and groups by points or by wins, then games difference. Everything else, the head-to-head split, open ties, moves and the close, is shared, so the ladder, the close screen and the automatic close can't rank differently.
- **Points travel only where they rank.** `LadderStanding.points` is set in Bonus points and null in Standard; the ladder shows a Pts column, and the close screen each Team's points, only when it has them.
- **Foursomes always ranks by games banked.** A tie the Players settle themselves, by a coin toss or otherwise, and they say who goes up and who down when they submit the scores (#14).
- **Existing Leagues are Standard.** The migration sets it on every Divisional League of Teams.

## Considered

- **A per-Stage or changeable mode.** Rejected by the owner: it is chosen at creation only, so a season isn't ranked two ways.
- **Bonus points for the Walkover's winner.** Rejected by the owner: a Walkover is a Game not played, so nobody gets a point for it.
- **A play-off Game to settle a tie.** Rejected by the owner: the tied Teams' own result decides, then the Manager.
- **Points stored with each Score.** Rejected: Standings are computed on every read (#10), so a corrected Score or a Void changes points at once, with nothing to keep in step.

## Consequences

- The setup form asks for the mode for a Divisional League of Teams, and the League's format line says "Bonus points" when it is.
- A Bonus points ladder is a column wider. It still fits a phone card (Storybook's `BonusPoints` story checks it).
- The mock's Summer Ladder is a Bonus points League, so its screens are snapshotted with points.
