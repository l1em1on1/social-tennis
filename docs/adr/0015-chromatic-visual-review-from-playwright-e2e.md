# Visual review with Chromatic, captured from the Playwright E2E suite

ADR-0014 made Playwright the frontend test seam. It checks behaviour and accessibility, but nothing notices when a screen *looks* different: a token change, a broken layout, a badge in the wrong colour. We add Chromatic for visual review, and feed it from the suite we already have rather than a second set of tests.

- **Capture.** The E2E specs import `test` and `expect` from `@chromatic-com/playwright` instead of `@playwright/test`. Each test writes a DOM archive (DOM, styles, assets) of its final page to `web/test-results/chromatic-archives`, and `takeSnapshot` adds mid-test captures where a test passes through a screen worth reviewing (the `/dev/ui` showcase).
- **Mock project only.** Snapshots are taken in the `mock` project, whose data is fixed by the MSW handlers. The `real` project sets `disableAutoSnapshot`: its Users are unique per run and its data changes, so its snapshots would differ every build. For the same reason the mock project logs in as `player@example.com`, not a random `+tag` address. The real project also writes to its own `test-results/real`: the plugin keeps one `<test title>.stories.json` per test under `<outputDir>/chromatic-archives`, and with a shared directory the real project's empty lists overwrote the mock project's, so Chromatic received no snapshots.
- **Upload.** A `chromatic` compose service (profile `e2e`, same Playwright image, which has git) runs `npx chromatic --playwright` over the archives. It mounts the whole repo because the CLI reads git history to link builds to commits and find baselines. Like everything else, it runs in Docker (ADR-0005).
- **Gate.** CI runs it after a passing E2E run, with `--exit-zero-on-changes`: visual changes don't fail the job. Chromatic's own "UI Tests" status check on the PR holds the merge until a reviewer accepts or rejects the changes in Chromatic. The step is skipped while the `CHROMATIC_PROJECT_TOKEN` secret is unset, so the repo works without an account.

## Considered Options

- **Storybook + Chromatic.** Component-level stories, the canonical Chromatic setup, with modes for light and dark. Not rejected but deferred: it is a second toolchain, and the shell and screens are exercised by E2E already. It becomes worth it once the component set grows (Phase 1 screens), and is the planned way to snapshot dark mode.
- **Playwright's own `toHaveScreenshot`.** No service, but baseline PNGs live in the repo and must be regenerated in the exact same browser image; reviewing a diff means reading image files in a PR. Chromatic gives a review UI with per-change accept/reject and keeps baselines out of git.
- **Snapshot the real project too.** Rejected for now: nondeterministic data. Revisit if the real project gets seeded, fixed fixtures.

## Consequences

- Every E2E test in the mock project is also a visual test; adding a spec adds a snapshot. A spec that ends on a page not worth reviewing opts out with `test.use({ disableAutoSnapshot: true })`.
- Dark mode is not snapshotted: Chromatic documents colour-scheme modes for Storybook only, and a Playwright archive is replayed without the test's `emulateMedia`. The showcase spec therefore turns auto snapshots off and takes explicit ones in light mode only; dark contrast stays covered by axe.
- A `test.use({ disableAutoSnapshot })` inside a spec overrides the project-level setting, including the real project's opt-out. Set it to `true` there, never to a computed value that can be `false`.
- The e2e CI job checks out the PR's **head** commit with full history, not GitHub's merge commit, as Chromatic recommends for baselines. The E2E run therefore tests the branch as pushed, not the result of merging it.
- Visual changes need a reviewer in Chromatic, in addition to code review on GitHub.
