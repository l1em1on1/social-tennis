# How-to guides and wireframes

Published at <https://l1em1on1.github.io/social-tennis/> whenever this folder changes on `main` (`.github/workflows/pages.yml`). Static files, no build.

## How-to guides

For the people who use the app (#251): [`index.html`](index.html) leads to one guide each for [members](guide-member.html), [League managers](guide-league-manager.html) and [Club managers](guide-club-manager.html). Each reads as a page, and presents one step at a time: the button at the top, or `?present` in the address (`guide-member.html?present#score` starts at a step). Arrow keys, the buttons or a swipe move; Escape leaves.

- **Words:** edit the HTML. Name buttons and headings exactly as the app does.
- **Pictures** (`guide/img/`) are the real app's, telling one story with made-up people. Take them again when a screen changes. They need an empty database, so this drops the local `tennis` one:

  ```sh
  docker compose --profile e2e build api web-e2e
  docker compose --profile e2e rm -sf api web-e2e
  docker compose up -d --wait postgres
  docker compose exec -T postgres psql -U tennis -d postgres -c 'DROP DATABASE IF EXISTS tennis'
  docker compose --profile e2e up -d --wait api web-e2e
  docker compose --profile e2e run --rm --no-deps -v "$PWD/docs/ui/guide/img:/guide" e2e \
    npx playwright test --config playwright.guide.config.ts
  ```

  The story is `web/e2e/guide/screens.guide.ts`. It isn't a test and CI doesn't run it. It follows today's date, so update the date at the foot of the guides and any they mention, and drop pictures no guide uses.

## Phase 1 wireframes

Clickable mobile wireframes of every Phase 1 screen (issue #41), designed before the screens are built.

**Open them at <https://l1em1on1.github.io/social-tennis/phase-1-wireframes.html>**, or open [`phase-1-wireframes.html`](phase-1-wireframes.html) from a clone. It is one self-contained file: no build, no server, nothing to install. Pick a screen on the left, then flip its **State** and **Viewer** (Player, Parent, Manager, Admin). Everything inside the phone is clickable. The notes panel on the right gives each screen's decisions and the open questions for the walkthrough.

The colours are the app's own tokens from `web/src/app/globals.css` (ADR-0014), and the shell (header, "Playing as" switcher, bottom nav, primitives) mirrors what #40 shipped, so the wireframes look like the real app in both light and dark.

Each screen has a deep link: `phase-1-wireframes.html#<screen>` or `#<screen>--<state>`, e.g. `#score--new`, `#home--held-up`, `#match--locked`. Use these from tickets. They work on the published site too: `https://l1em1on1.github.io/social-tennis/phase-1-wireframes.html#score--new`.

### Screens

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

### Score entry

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
- The date played must fall inside the month. **Built in #38 as:** from the Stage's first day to today in the Club's time zone, with no bound at the month's end, since a Stage stays open until it closes (ADR-0016).

**Superseded by ADR-0031 (2026-11-21):** the ladder (P5), Start (M4) and Close (M6) screens show Divisions of 2 with a Bye. Divisions now hold about 4 Teams, everyone in a Division plays everyone, and there is no Bye.

### Decisions taken in the wireframes

- **The "Ladder" tab becomes "Leagues".** A Player can be in several Leagues, so the tab opens a list (mine, then joinable), and each League opens its ladder. This changes one nav entry from #40's shell when #5 lands.
- **"Stage held up" is a banner**, on Home and on the ladder, that names what is blocking the close. Players see the same to-do items the Manager does, read-only.
- **Reordering the starting ladder uses up and down buttons**, not drag-and-drop: easier with a thumb and with a screen reader.
- **Month close is reached from the to-do list**, because it is blocked until that list is empty.
- **The last Admin can't remove their own Admin role.**

### Walkthrough feedback

Feedback is ongoing: the wireframes are a living design, updated as screens are built and as the club reacts to them. Send people the published link and record what comes back in the table. The open questions are listed per screen in the notes panel; the main ones, still open:

1. Which score entry variant: A, B, or C (or a mix)? #38 builds A, the plainest reading of its ticket, until the club picks; changing it touches only `web/src/components/score-entry-form.tsx`.
2. The tiebreak rule for short sets: at 5–5 to 6–5, or at 6–6 to 7–6? #38 builds 5–5 to 6–5, as CONTEXT.md (Set) has it.
3. Home deadline: a date ("by 30 Nov") or a countdown ("9 days left")?
4. Ladder: the whole ladder, or just your Division and the ones either side?
5. Should a walkover need a reason? For now an unplayed Game is only marked not played (a Void), and counts for nothing (owner, 2026-11-22, #10); Walkovers wait for scoring modes (#214).

| Date | Who | Feedback | Outcome |
| --- | --- | --- | --- |
| 2026-11-13 | Product owner | Likes the UI as wireframed. Feedback will be ongoing, not a one-off walkthrough. | #41 closed. Wireframes published on GitHub Pages so they are easy to share. The five questions above stay open. |
