---
status: accepted
---

# A Foursomes Player is a Team of one, and a Side can hold two

Foursomes (#14, ADR-0013) is a ladder of individual Players in Divisions of 4. Each Stage a Division plays 3 Games with rotating partners, and each Player banks the games their Side won. Everything else is built on Teams: the ladder's places, a Game's Sides, Standings, the close, Relegation, who may enter a Score. Foursomes had no Teams, so it couldn't start.

## Decision

- **Each Foursomes participant is a Team of one, made when they join,** as in singles (`LeagueTypeTeams.HasTeamsOfOne`). It holds their place on the ladder and puts them on a Side. It is never a pairing: nobody is paired in Foursomes, and `TeamSize` stays 0, so a Foursomes League still has no Division size and no Standings mode. The `FoursomesSides` migration gives existing participants theirs.
- **A Side can hold two Teams.** A `GameSides` row is keyed by its Game and Team, not by its Side number. A Foursomes Side is two rows: the pair for that Game.
- **The 3 Games are a fixed rotation** of the Division's places: 1+2 v 3+4, 1+3 v 2+4, 1+4 v 2+3 (`Ladder.FoursomesRotation`). Each Player partners each of the others once and plays against each twice. Side 1 is the pair with the first place.
- **Standings rank by games banked, and nothing else** (`Standings.RankFoursomes`; owner, 2026-11-26: "just use the marker, no need to overcomplicate"). Players level on games share a rank. Where that decides a move, it is an open tie, which the Players will mark when they enter the scores (#14 part 3). Until then the Manager orders it at the close, as every tie is ordered today.
- **A Game not played hands the Division to the Manager** (owner, 2026-11-26). Every Player shares first, so the Division is one tie, for the Manager to order at the close.
- **The responses grow, they don't change shape.** `LadderGame` and `GameSideSummary` keep one Team id per Side and add the partner's (`firstPartnerTeamId`, `secondPartnerTeamId`, `partnerTeamId`), null outside Foursomes. A Match screen's Side lists both Players.
- **Two things wait on the owner, so they are refused for now:** a Player joining a running Foursomes ladder at the close (none is offered), and a Manager's mid-Stage move (409 `foursomes-fixed`). Either would break a Division of 4.

## Considered

- **Places and Sides that hold a Player instead of a Team.** Every ladder query, Standings, Relegation, the close and the moves would need a second path. Teams of one reuse them all.
- **One Side row with a second, nullable Team column.** Every check of the form "is this Player's Team on a Side" would read only the first column and silently miss the partner. The fourth place is never first on a Side in the rotation, so that Player couldn't enter a Score. Two rows make an unchanged query either right or fail loudly.
- **A Team per pair.** A participant belongs to one Team, and in Foursomes everyone has three partners a Stage.

## Consequences

- The ticket's "with no Team created" now means no pairing: a Foursomes League still has no Teams of two, and the Manager pairs nobody.
- A Down migration can't restore the old key once a Foursomes Game exists, as its Sides have two rows.
- The web shows Foursomes as it shows singles until its screens are built (#14 part 2): its start page still says Foursomes can't start.
