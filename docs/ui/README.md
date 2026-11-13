# Phase 1 wireframes

Clickable mobile wireframes of every Phase 1 screen (issue #41), designed before the screens are built.

**Open [`phase-1-wireframes.html`](phase-1-wireframes.html) in a browser.** It is one self-contained file: no build, no server, nothing to install. Pick a screen on the left, then flip its **State** and **Viewer** (Player, Parent, Manager, Admin). Everything inside the phone is clickable. The notes panel on the right gives each screen's decisions and the open questions for the walkthrough.

The colours are the app's own tokens from `web/src/app/globals.css` (ADR-0014), and the shell (header, "Playing as" switcher, bottom nav, primitives) mirrors what #40 shipped, so the wireframes look like the real app in both light and dark.

Each screen has a deep link: `phase-1-wireframes.html#<screen>` or `#<screen>--<state>`, e.g. `#score--new`, `#home--held-up`, `#match--locked`. Use these from tickets.

## Screens

| # | Screen | Link | States | Built in |
| --- | --- | --- | --- | --- |
| P1 | Log in | `#login` | default, sent, bad-link | #40 (done) |
| P2 | Home | `#home` | default, held-up, bye, empty, loading, error | #42 |
| P3 | Match (score and history) | `#match` | score-needed, scored, locked, walkover, loading, error | #38 |
| P4 | Score entry | `#score` | new, edit, server-error; 3 layout variants × 4 League formats | #38 |
| P5 | League ladder | `#ladder` | default, foursomes, held-up, past-month, not-started, loading, error | #7, #10 |
| P6 | Leagues (mine and joinable) | `#leagues` | default, empty, loading, error | #5 |
| P7 | My profiles | `#profiles` | parent, single | #40 (done), #4 |
| M1 | Manage | `#manage` | default, empty | #5 |
| M2 | League setup | `#setup` | new, invalid | #5 |
| M3 | Players (add, requests, Teams) | `#players` | default, add-player, empty | #4, #5, #6 |
| M4 | Start the league | `#start` | singles (with Bye), foursomes (cut) | #7 |
| M5 | To-do | `#todo` | default, walkover, empty, loading, error | #10 |
| M6 | Close the month | `#close` | ready, held-up, closed | #10 |
| A1 | Club and roles | `#admin` | default, no-club, error | #18 |

## Score entry

The screen Players use most, so it gets three structurally different layouts to choose from. All three share one set of rules, written as plain functions in the file (`setResult`, `evaluate`) so #38 can port them as the client-side hints:

- **A · All sets:** every set on one screen, two steppers per set.
- **B · Pick scoreline:** per set, tap who won, then tap the scoreline (6–0 … 6–4, 6–5 TB).
- **C · Set by set:** one set per step with oversized steppers, then a review.

The **League format** chips switch between one set of 6, best of 3, best of 3 with a match tiebreak, and one set of 9. **Try a case** fills in straight sets, a match tiebreak, a 5–5 tiebreak, a retirement, and an invalid 6–5, which covers #41's acceptance list.

Rules as wireframed (to confirm with the club, then enforced by the server in #38):

- A set ends when one side reaches 6 (or 9) games with a 2-game lead.
- At 5–5 (or 8–8) the set goes to a tiebreak, first to 7 and 2 clear. The set is recorded as 6–5 (9–8) plus the tiebreak points, and the tiebreak row only appears at that score.
- In best of 3, the third set only appears at one set all. Where the League allows it, it is a match tiebreak, first to 10 and 2 clear.
- A retirement keeps the sets as they stood (incomplete is fine) and needs an explicit winner.
- The date played must fall inside the month.

## Decisions taken in the wireframes

- **The "Ladder" tab becomes "Leagues".** A Player can be in several Leagues, so the tab opens a list (mine, then joinable), and each League opens its ladder. This changes one nav entry from #40's shell when #5 lands.
- **"Stage held up" is a banner**, on Home and on the ladder, that names what is blocking the close. Players see the same to-do items the Manager does, read-only.
- **Reordering the starting ladder uses up and down buttons**, not drag-and-drop: easier with a thumb and with a screen reader.
- **Month close is reached from the to-do list**, because it is blocked until that list is empty.
- **The last Admin can't remove their own Admin role.**

## Walkthrough feedback

To fill in after walking through with the club's Manager (an acceptance criterion of #41). The open questions are listed per screen in the notes panel; the main ones:

1. Which score entry variant: A, B, or C (or a mix)?
2. The tiebreak rule for short sets: at 5–5 to 6–5, or at 6–6 to 7–6?
3. Home deadline: a date ("by 30 Nov") or a countdown ("9 days left")?
4. Ladder: the whole ladder, or just your Division and the ones either side?
5. Should a walkover need a reason?

| Date | Who | Feedback | Outcome |
| --- | --- | --- | --- |
| | | | |
