# The PoC is hosted on Azure Container Apps and Neon, scaled to zero

The app is a proof of concept, and it should cost as close to nothing as possible to run until the club depends on it. So it runs on **Azure Container Apps** (Consumption plan, UK South) with **Neon** for Postgres, both scaled to zero when idle, images in **GitHub Container Registry**, all created by Terraform in `infra/`. Inside the free grants this is about £0 a month. The original go-live plan (issue #26: AWS EC2 + RDS) would cost roughly $35–45 a month once sign-up credits ran out.

The topology doesn't change (ADR-0001): `web` has public ingress, `api` has internal ingress only and is reached as `http://api` inside the Container Apps environment, and the database is external. The address is the `*.azurecontainerapps.io` hostname Azure assigns; there is no custom domain yet.

Free allowances, as found on 2026-11-13 (re-check at sign-up): Container Apps gives 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests a month per subscription; Neon's free plan gives 1 GB of storage and 100 compute-hours a month per project.

## What this accepts

- **Cold starts.** After about five idle minutes both apps stop and Neon suspends. The next visit waits for web, then the API (which checks migrations on startup), then the database. A club app is visited every few hours, so most first visits are cold.
- **No always-running process.** Scheduled work (Stage auto-roll, token cleanup, retention) can't be a hosted service inside the API. It runs as a **Container Apps Job** on a cron schedule, using a job entry point in the API image.
- **Probes must not touch the database.** A liveness probe that queries Postgres every few seconds keeps Neon awake and spends the compute allowance. `/health` answers without the database; `/health/ready` checks it.
- **No real email.** Sending needs a domain for DKIM. Until there is one, sign-in links are read from the API's logs, which works for a handful of testers and not for the club (#19).

## Considered Options

- **AWS EC2 + RDS** (the original #26) — rejected for cost. Managed backups and a London region, at a price a PoC doesn't justify.
- **One small VPS running the compose file** (Hetzner, about €6–7 a month after its 2026 price rise) — the simplest option, with no cold starts and scheduled jobs working as first designed. Rejected only because it isn't free. It is the natural next step if cold starts become the complaint.
- **Oracle Cloud Always Free** — free and always on, but free capacity is often unavailable, idle instances can be reclaimed, and the size of the allowance was disputed in 2026.
- **Container Apps with one replica always running** — removes the cold start but leaves the free grant. Kept as a switch, not a default.

## Consequences

- An `always_on` Terraform variable sets the minimum replicas to 1, so removing cold starts is a one-line change.
- Nothing ties the app to this host: the images are ordinary containers and the database is plain Postgres, so a VPS or AWS later reuses both.
- Azure's probes are HTTP calls made by the platform, so the shell-less runtime image (#25) needs no in-container healthcheck in production.
- Neon's pooled connection string can't be used for the startup migration, which holds a session advisory lock. The API uses the direct connection.
- Terraform's Neon provider is community-maintained. The database is its own module, so creating the Neon project by hand and passing in a connection string is a local swap.
- A subscription budget alert at £5 is part of the infrastructure, so leaving the free grant shows up as an email.
