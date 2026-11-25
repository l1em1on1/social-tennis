# Frontend foundation: design tokens, owned components, MSW, Playwright E2E

The web app gets one foundation before feature screens are built, so that every Phase 1 ticket can ship its screen alongside its API instead of leaving UI to the end:

- **Design tokens** as Tailwind 4 `@theme` variables in `globals.css`, with light and dark values. Screens use tokens, never raw colours.
- **Owned UI primitives** in `web/src/components/ui` (Button, Field, Select, Card, Badge, Sheet/Dialog, EmptyState, ErrorState, Skeleton), plus domain-level result badges (played, retirement, walkover, void, pending). Owned rather than a styled third-party kit: the surface is small, mobile-first, and must express the domain's states exactly.
- **A mobile-first app shell**: bottom navigation (Home, Ladder, Profiles), Manage and Admin entries by role, and a "playing as" Player switcher held server-side so a User with several Players (CONTEXT: User) acts as one at a time.
- **MSW in Node mode** with schema-typed handlers (`openapi-msw` over `schema.d.ts`), behind an explicit dev/test flag and excluded from production builds — ADR-0009 put into practice, so screens can be built before or without the API.
- **Playwright E2E as the frontend test seam**, run as a compose service (ADR-0005) against the real stack, with automated accessibility (axe) checks. This settles the seam issue #1 left open. Each feature ticket adds its happy-path E2E; domain rules stay tested at the API seam.

Library specifics (package names, Next.js 16 instrumentation hooks for MSW, Playwright image tags) are confirmed against current docs — Context7 and `web/node_modules/next/dist/docs/` — when the foundation is built, per CLAUDE.md.

## Considered Options

- **UI per screen, after all API tickets** — rejected: UI is what the club actually uses, and leaving it to the end is how it gets neglected.
- **A full component kit** (e.g. a pre-styled library) — rejected for now: more surface than a handful of mobile screens need, and harder to bend to domain states. Headless primitives may be adopted underneath owned components where accessibility is hard (dialogs, sheets).
- **Component tests only (Vitest + Testing Library)** — rejected as the primary seam: server components and server actions do most of the work here, and those are exercised honestly only end-to-end.

## Consequences

Every Phase 1 ticket carries UI acceptance criteria and is done only when its screen and its E2E happy path ship. Mobile wireframes for the Phase 1 screens are produced first so screens are designed rather than improvised. CI gains an E2E job in `web.yml`.

**Amended (2026-11-20): real-API sign-in without an inbox.** The E2E stack sends no email, so a test signs in by submitting the real login form, then replacing the newest unused sign-in link's stored hash with the hash of a token it knows, and redeeming that. Everything but delivery runs for real, and the app has no test-only endpoint. The cost is that one helper, `e2e/support/auth.ts`, knows two table names (one since 2026-11-25: roles are granted through the API, as an Admin would, rather than written to `Users`; #110).

**Amended (2026-11-24): responsive from `md` (#225).** The owner wants a better desktop version. Mobile-first stays: below Tailwind's `md` breakpoint (48rem, 768px) every screen is as it was. From `md` up:

- **Navigation:** the main navigation is a top bar in the header, rather than tabs at the bottom. Between `md` and `lg` (1024px) it shows icons only, with each label still read out, so the header fits a tablet held upright. From `md` a long "Playing as" name is cut short, in full on hover, so the full labels from `lg` can't overflow either. It is one `<nav aria-label="Main">` in the header either way, fixed to the bottom of a phone's screen by CSS, so it comes before the content in reading order.
- **Content:** it widens to `max-w-5xl`. Pages that gain from the width lay out in columns: Home's League cards, the Leagues list, Manage's Club cards (Leagues and the join link | who's in it), a League's Manager page, the ladder's Divisions, and Admin's table-like rows.
- **Narrow pages:** forms, the Match screen, Profiles and the join page stay a centred `max-w-2xl` column (`PageColumn`).

Tap targets stay 44px at every width. A left sidebar was the other option; a top bar was chosen because there are only three to five entries, and it leaves the width to lists and tables.

**Amended (2026-11-24): the Clay court look (#226).** The owner wanted colour and a livelier design. Three directions were tried on the real screens, with the same fixed data, in light and dark: Tennis-ball lime, Clay court and Grass-court purple (branch `prototype/design-directions`). The owner picked Clay court.

- **Tokens.** A cream page, terracotta for the brand and every action (`accent`), warm neutrals, and a warm `shadow-card`. Dark mode uses deep brown with a lighter terracotta. The status colours are unchanged. Every text pairing in use is at least 4.5:1, and `border-strong` at least 3:1, in both themes (the figures are in `globals.css`).
- **Type.** Page titles (`h1`) and card titles are in Fraunces, a warm variable serif loaded through `next/font`. Everything else stays in Geist.
- **Shape and depth.** Buttons and icon buttons are pills; form fields stay rounded rectangles. Cards are rounder, with a soft shadow and a small terracotta mark before the title. Empty states sit on a muted fill with a terracotta dashed edge.
- **Brand.** The header is ruled in terracotta and carries a ball mark in the brand colour, as do the login and sign-in pages and the app icon. The current tab has a terracotta bar on a phone and a pill from `md`.

**Amended (2026-11-24): every component's accessibility is checked in Storybook (#232).** A page, `/dev/ui`, rendered every primitive so the e2e suite could run axe over it in light and dark. It existed only in dev or mocked builds, which made the mocked build a different app from the one shipped. The owner's rule (2026-11-24) is that every environment runs the same code, so the page is gone. The same axe check now runs on every Storybook story, in both colour schemes, after its play. That covers more than the page did: the forms, the ladder and the Match components too. This replaces the owner's decision of 2026-11-18 to keep the page.

- **Considered:** serving `/dev/ui` in every environment to Admins only. It would put a page nobody needs in production.
- **Considered:** Storybook's own test addons. Playwright and `@axe-core/playwright` are already here, and the e2e suite's check is reused as it is.
- **Consequence:** a pairing that no story shows isn't checked in dark mode, so a new component comes with a story, and a new pairing on a card goes into `UI/On a card`.

**Amended (2026-11-24): the mock API is a separate service (#233).** MSW no longer runs inside the app behind a flag. Both e2e projects run the production image; `web-mock`'s `API_URL` is the mock API (ADR-0033).

**Amended (2026-11-25): a word under an icon, Division marks, and space between buttons (#255).** The owner asked for icons in place of row buttons such as Leave out and Settle, for a smaller way to show a Division beside names, and for more space between stacked buttons.

- **An icon with a small word**, on one 44px button, where the icon isn't plain alone: a phone has no tooltip, and a person with a minus could be Leave out or Remove. Arrows are icons only. Every one is as high as the pill it replaces and narrower.
- **Words only** on a page's main action, and where a slip can't be undone by whoever it's about: a League's Approve and Reject.
- **A row that only opens something is the link**, with a chevron: Settle opens the Match screen.
- **A Division beside names** is its number in a ring, read out as "Division 1". Titles, sentences and lists of options keep the words. Considered: letters (A, B, C). Divisions are numbered in the API and on every screen, so that would be a change of its own.
- **16px between buttons, one above another**, and 8px from a button to its own hint. `e2e/support/spacing.ts` checks it at 360px.

**Amended (2026-11-25): the Hard court look (#254), replacing Clay court.** The owner found Clay court "too much like the financial institution website": serif titles on cream, one muted accent and hairline outlines. Five new looks were tried over the real app, each changing type, shape, colour and header together: Hard court, Sticker, Strawberries, Seventies and Scoreboard (branch `prototype/looks-2`). The owner picked Hard court.

- **Tokens.** A pale blue page, the court's blue (`court`) for the header and every card's title, ball yellow (`ball`) for the main action on a page, and blue (`accent`) for links, secondary buttons and marks. Dark mode is a night court: deep navy, the same yellow, a lighter blue. The status colours are unchanged. Every text pairing in use is at least 4.5:1, and `border-strong` at least 3:1, in both themes (the figures are in `globals.css`).
- **Type.** Big Shoulders, tall like a scoreboard's, for the app's name, page titles, card titles and a Game's score; Figtree for everything else. Page titles and names keep their own capitals: "CHRIS LEE V ALEX CARTER" reads badly. Card titles and the app's name are in capitals.
- **Shape.** A card's title sits on a band of court blue ruled with a white line, as the header is. Corners are squared off; badges and round icon buttons stay round. Primary buttons are yellow with a shadow under their edge; secondary ones are outlined in blue.
- **On the court.** The header's own parts (the switcher, the tabs from `md`) take the court's colours through an `on-court` utility that redefines the tokens, so the shared components need nothing of their own.

