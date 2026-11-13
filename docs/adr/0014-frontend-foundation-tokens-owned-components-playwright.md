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
