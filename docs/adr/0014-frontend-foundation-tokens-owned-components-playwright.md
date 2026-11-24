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

**Amended (2026-11-20): real-API sign-in without an inbox.** The E2E stack sends no email, so a test signs in by submitting the real login form, then replacing the newest unused sign-in link's stored hash with the hash of a token it knows, and redeeming that. Everything but delivery runs for real, and the app has no test-only endpoint. The cost is that one helper, `e2e/support/auth.ts`, knows two table names.

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
