---
status: accepted
---

# Divisions hold a League-set number of Teams, everyone in one plays everyone, and the ladder has no Bye

ADR-0013 made a Divisional ladder of Divisions of 2: one Game per Division per Stage, with a Bye for the one left over. It rejected "Divisions of about 4 Competitors, round-robin" as not the club's format. That was a misreading. The owner (2026-11-21, on #7): "multiple divisions normally fixed number usually 4 teams per division, but if the numbers are uneven we might put an extra or fewer teams in any division", and "we shouldn't have byes in the ladder". This supersedes ADR-0013's Divisions of 2 and its Bye for the Team-based Types (Singles and Doubles).

- **Division size is a League setting:** Teams per Division, 4 by default, from 2 to 8, chosen when the League is created. A Divisional League of Teams has one. Knockout has none, since it has no Divisions. Foursomes has none either, since its Divisions are always 4 Players (ADR-0013's amendment, #14).
- **The start proposes Divisions of about that size,** split as evenly as possible with the bigger Divisions at the bottom (`Ladder.ProposeDivisions`, mirrored in `web/src/lib/league.ts`). The proposal makes as many Divisions as the number of Teams rounds to, and never a Division under 2. For example, with Divisions of 4: 6 Teams make 3 and 3, 9 make 4 and 5, and 10 make 3, 3 and 4. The Manager can change the number of Divisions, move the line between two of them, reorder the ladder and leave Teams out. The confirm sends the Division sizes, each at least 2, adding up to the ladder.
- **No Bye.** Teams that don't divide evenly join a Division, making it a Team bigger.
- **Everyone in a Division plays everyone else once per Stage:** a Division of 3 has 3 Games, 4 has 6 and 5 has 10. Side 1 is the Team higher on the ladder.
- **Relegation is unchanged:** each Division's top Team moves up one Division and its bottom Team moves down one, and the ends of the ladder stay put (#10). Standings in a Division use CONTEXT.md's order: matches won, then games difference, then head-to-head, then the Manager.
- **Unchanged:** Knockout Byes in the Bracket (#12, #13), and Foursomes' Divisions of exactly 4 Players with no Bye.

## Considered Options

- **Keep Divisions of 2, and make the ladder even.** This was rejected: it's not the club's format, which is Divisions of about 4.
- **Divisions of exactly the League's size, with the remainder left out.** This was rejected: the owner wants uneven numbers absorbed into Divisions, not players left out.
- **Let the Manager size every Division from scratch.** This was rejected as a default. The proposal does the arithmetic, and the Manager adjusts it.

## Consequences

- **Each place stores its Division** (ADR-0030, amended), since sizes vary.
- **A Stage holds many more Games:** 6 per Division of 4, against 1 per Division of 2. Arranging (#9) and Scores (#38) work per Game, so nothing there changes shape.
- **Leagues started before this:** the `DivisionSize` migration gives each place the Division it had under Divisions of 2, and an old Bye joins the Division above it, without new Games. Production had no started League.
- **The wireframes** (docs/ui) still show Divisions of 2 and a Bye. They predate this.
