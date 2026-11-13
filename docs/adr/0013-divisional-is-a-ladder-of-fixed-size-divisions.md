# Divisional Leagues are a ladder of fixed-size Divisions

A `Divisional` League is a ladder of numbered Divisions of a fixed size: 2 Competitors for Singles and Doubles Types (one Game per Stage), 4 individual Players for `Foursomes` (the three-Game partner rotation). Each Stage the top Competitor of every Division moves up one rung and the bottom one moves down; the ends of the ladder stay put. One Competitor left over when the numbers don't fit takes a Bye at the bottom of the ladder — no Game, no movement — rather than blocking the Stage or being promoted for not playing.

Chosen because this is how the club actually runs its leagues. The original model — Divisions of about 4 Competitors playing round-robin — was an assumption from early feature notes, corrected during the 2026-10-26 re-prioritisation grilling.

## Considered Options

- **Divisions of ~4 Competitors, round-robin** (the original design in #7/#10) — rejected: not the club's format.
- **Blocking the Stage until numbers fit** — rejected: one missing Player would stall the whole League.
- **A Bye counting as a win** — rejected: rewards not playing and lets the Bye decide a promotion.

## Consequences

Most Competitors move every Stage, so Relegation is the heartbeat of the League rather than an edge event. Fixture generation is trivial (1 or 3 Games per Division) and Standings in a 2-Competitor Division reduce to the Game's result — ties only arise in `Foursomes` or through unplayed Games, both of which go to the Manager. Stages can auto-roll or be rolled by the Manager, a per-League switch.

## Amendment (2026-11-13): starting ladder, and Foursomes has no Bye

**Starting ladder.** Starting a `Divisional` League proposes a ladder ordered by Competitor Rating (a Team's is the average of its Players'), ties broken by earliest join time — every participant's join time is recorded for this. The Manager may reorder Competitors or leave some out, and the League starts only when the Manager confirms: the Manager is always the final approval gate. Rating-first gives a sensible default with no effort; the Manager's override covers what Rating can't see (Rating defaults to 1, so early on the proposal is mostly join order).

**Foursomes has no Bye.** With Divisions of 4, between 1 and 3 Players can be left over, and sitting several out every Stage isn't how the club runs it. A `Foursomes` ladder always holds a multiple of 4 Players: when the count doesn't fit, the Manager decides at start who doesn't make the cut — in practice after trying to recruit Players to fill the gap. The single-Competitor Bye above still applies to Singles and Doubles Types.
