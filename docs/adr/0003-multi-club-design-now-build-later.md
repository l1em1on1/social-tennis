# Multi-club: design the schema now, build enforcement later

Add a `Club` entity now with FK relationships across `League`, `Social`, and `Player`, but defer tenant isolation enforcement (e.g. Postgres Row-Level Security), multi-club auth, and per-club admin/manager role scoping until a second real club is being onboarded. Chosen to avoid a painful schema migration later, without paying the cost of full multi-tenancy before it's needed for v1's single club.
