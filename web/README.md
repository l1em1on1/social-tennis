# web — Next.js BFF

See the [root README](../README.md) for how to run, develop, and test. **Everything runs in Docker** — never `npm` directly on the host (ADR-0005).

The browser talks only to this app; this app talks to the .NET API server-side through the generated client in `src/lib/api/` (ADR-0001).
