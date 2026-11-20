# The PoC is hosted on Azure Container Apps and Neon, scaled to zero

The app is a proof of concept, and it should cost as close to nothing as possible to run until the club depends on it. So it runs on **Azure Container Apps** (Consumption plan, UK South) with **Neon** for Postgres, both scaled to zero when idle, images in **GitHub Container Registry**, all created by Terraform in `infra/`. Inside the free grants this is about £0 a month. The original go-live plan (issue #26: AWS EC2 + RDS) would cost roughly $35–45 a month once sign-up credits ran out.

The topology doesn't change (ADR-0001): `web` has public ingress, `api` has internal ingress only and is reached as `http://api` inside the Container Apps environment (**Corrected 2026-11-20:** `web` uses the api app's internal https address; see the amendment below), and the database is external. The address is the `*.azurecontainerapps.io` hostname Azure assigns; there is no custom domain yet. (**Superseded 2026-11-14:** the app is served at its own domain, ADR-0021.)

Free allowances, as found on 2026-11-13 (re-check at sign-up): Container Apps gives 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests a month per subscription; Neon's free plan gives 1 GB of storage and 100 compute-hours a month per project.

## What this accepts

- **Cold starts.** After about five idle minutes both apps stop and Neon suspends. The next visit waits for web, then the API (which checks migrations on startup), then the database. A club app is visited every few hours, so most first visits are cold.
- **No always-running process.** Scheduled work (Stage auto-roll, token cleanup, retention) can't be a hosted service inside the API. It runs as a **Container Apps Job** on a cron schedule, using a job entry point in the API image.
- **Probes must not touch the database.** A liveness probe that queries Postgres every few seconds keeps Neon awake and spends the compute allowance. `/health` answers without the database; `/health/ready` checks it.
- **No real email.** Sending needs a domain for DKIM. Until there is one, sign-in links are read from the API's logs, which works for a handful of testers and not for the club (#19).
  **Superseded (2026-11-14):** there is a domain now, and email is sent from it. See ADR-0020.

## Considered Options

- **AWS EC2 + RDS** (the original #26) — rejected for cost. Managed backups and a London region, at a price a PoC doesn't justify.
- **One small VPS running the compose file** (Hetzner, about €6–7 a month after its 2026 price rise) — the simplest option, with no cold starts and scheduled jobs working as first designed. Rejected only because it isn't free. It is the natural next step if cold starts become the complaint.
- **Oracle Cloud Always Free** — free and always on, but free capacity is often unavailable, idle instances can be reclaimed, and the size of the allowance was disputed in 2026.
- **Container Apps with one replica always running** — removes the cold start but leaves the free grant. Kept as a switch, not a default.

## Consequences

- An `always_on` Terraform variable sets the minimum replicas to 1, so removing cold starts is a one-line change.
- A `long_cooldown` Terraform variable (added 2026-11-14) keeps an idle app for 30 minutes, not 5, so visits close together stay warm. The two apps running together use 0.5 vCPU and 1 GiB, which the free grant covers for about 100 hours a month; at 30 minutes per visit that is roughly six separate visits a day. Neon still suspends after about five minutes, so a warm API can still wait for the database to resume.
- Nothing ties the app to this host: the images are ordinary containers and the database is plain Postgres, so a VPS or AWS later reuses both.
- Azure's probes are HTTP calls made by the platform, so the shell-less runtime image (#25) needs no in-container healthcheck in production.
- Neon's pooled connection string can't be used for the startup migration, which holds a session advisory lock. The API uses the direct connection.
- Terraform's Neon provider is community-maintained. The database is its own module, so creating the Neon project by hand and passing in a connection string is a local swap.
- A subscription budget alert at £5 is part of the infrastructure, so leaving the free grant shows up as an email.

**Amended (2026-11-20): what running it taught.**

- **`web` reaches `api` over https** (#111), at the api app's internal address, and the api ingress refuses plain http. Traffic never leaves the environment either way, but https keeps session tokens off the wire in clear inside it too. The short name `http://api`, which the paragraph above describes, is plain http, so `web` no longer uses it.
- **Each app runs at most one replica**, because the API migrates when it starts. EF Core's guidance fits migrating at startup to development, tests and a single instance. Before the API scales past one instance, migrations become a scripted step (`dotnet ef migrations script`).
- **The migration lock is taken on the app's own database**, not on `postgres`, which a Neon role may not be allowed into.
- **The environment declares the Consumption workload profile** (#150). Azure adds the profile, and assigns every app and job to it, whether or not Terraform declares them. Declared, a plan with nothing to do reads "No changes" instead of removing them each time.
- **A nightly dump, kept outside Neon** (#137). Neon's free plan keeps 6 hours of point-in-time history, so a job dumps the database to its own storage account, kept 14 days. Terraform manages that account through Azure Resource Manager only, so the deploy identity needs no data-plane role. The deploy identity can't grant roles either, so the owner gives the job's identity its role on the container, once.
