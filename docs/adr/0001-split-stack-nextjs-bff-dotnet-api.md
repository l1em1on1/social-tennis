# Split stack: Next.js BFF + ASP.NET Core Web API

We chose a split stack over a single Next.js full-stack app: Next.js (App Router) as a thin BFF for the mobile-first UI, proxying to a separate ASP.NET Core Web API (EF Core + Postgres) that owns the domain. Chosen because the team has deep .NET experience and shallow Next.js-as-backend experience, the project wants to be API-first (room for a future native client or third-party integrations), and planned multi-club support raises concurrent-load concerns where .NET's throughput characteristics matter more than at single-club scale.

## Considered Options

Single Next.js full-stack app — simpler ops and one language end-to-end, but the backend would be built in a less-familiar stack with a less clean API boundary.

## Consequences

Requires a BFF proxy layer (the browser never calls the .NET API directly, to avoid CORS and keep auth tokens server-side) and an OpenAPI-generated TS client to keep the two languages' types in sync.
