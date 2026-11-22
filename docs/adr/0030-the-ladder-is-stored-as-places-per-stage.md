---
status: accepted
---

# A Stage's ladder is stored as ordered places, each with its Division

Starting a Divisional League (#7) has to record where every Competitor stands. Relegation (#10) then derives each next Stage's ladder from the last one, and Games, Substitutes and Scores hang off the Divisions (#11, #38). The ladder needs a shape that later tickets can build on.

**Each Stage has its own ordered ladder: a row per Competitor, numbered from 1 at the top** (`LadderPlaces`: Stage, position, Division, Team). A Division is a run of consecutive places. Each place stores its Division, because Divisions vary in size (ADR-0031).

**A Game has Sides, not two Team columns** (`Games` and `GameSides`). Side 1 is the Competitor higher on the ladder. A Game records its Stage and Division.

**Every row carries its League's id,** and composite foreign keys tie places, Games and Sides, and the Teams they name, to one League, as participants already are (#6). A Team on a ladder or in a Game can't be deleted; the endpoints refuse to break it up first, with a typed 409.

**Started means Stage 1 exists.** There is no separate "started" flag to keep in step. A unique (League, Stage number) backs the League lock against a second start.

## Considered Options

- **Divisions as rows, with their Competitors under them.** This was rejected: it is a second copy of the order. Relegation would then have to keep Divisions and positions in step. With places alone, Relegation writes a new ordered list, and a Manager's move (#10) is a swap.
- **A Game with `FirstTeamId` and `SecondTeamId`.** This was rejected: Foursomes (#14) has Sides that are a pair of Players for one Game, not a Team, and Socials (#16) assemble Sides each time. Sides let those add Players to a Side without reshaping Games.
- **A `StartedAt` on the League.** This was rejected: it duplicates "Stage 1 exists".

## Consequences

- **Not built yet.** Foursomes (#14) will place Players rather than Teams, with Divisions of 4 and no Bye. A Social's Games (#16) will have no Stage. Each adds nullable columns, not new shapes.
- **Each Stage's ladder is kept,** so past Stages can be shown as they finished (wireframe P5, "past months").
- **Reading the current ladder** means taking the League's highest Stage number.

**Amended (2026-11-21): each place stores its Division (ADR-0031).** The first version stored positions only and read Divisions of 2 from them, with the Bye at the bottom (`Ladder.DivisionOf`). Divisions now vary in size, set by the Manager at the start, so a place has its own `Division` column, and nothing derives it. There is no Divisional Bye.
