# Hits & Giggles

Mobile-first web app for organizing league and social tennis games at a club — Divisional and Knockout leagues, availability-based match scheduling, score agreement, and social sign-ups.

- Domain glossary: [CONTEXT.md](CONTEXT.md) · Decisions: [docs/adr/](docs/adr/) · Spec and tickets: [GitHub issues](https://github.com/l1em1on1/social-tennis/issues)
- Stack: Next.js BFF (`web/`) → ASP.NET Core Web API (`api/`) → Postgres (ADR-0001)

## Prerequisites

**Docker. Nothing else.** No .NET SDK, Node, or npm on the host — every command below runs in a container (ADR-0005). Edit with any editor on the host, and run the commands below from its terminal. Without the .NET SDK on the host, an editor's C# support can't load the projects, so expect syntax highlighting rather than C# IntelliSense. Claude Code works in a [Docker Sandbox](.sbx/README.md), which has the toolchain and language servers.

## Run the stack

```sh
docker compose up --build
```

Postgres, the API, and the web app start together; migrations apply automatically. Open http://localhost:3000.

A new database has no Club. Sign in as `admin@example.com`, which the compose file names as an Admin (`Auth__AdminEmails`, ADR-0022): the sign-in link is printed in the api container's log. Then create the Club on the Admin tab. In production the Admins are the addresses in the `ADMIN_EMAILS` GitHub secret.

## Develop (hot reload)

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up
```

Source is bind-mounted: the API restarts on change (`dotnet watch`), the web app hot-reloads (`next dev`).

### Web only, against a mocked API

```sh
API_URL=http://mock-api:8080 docker compose -f docker-compose.yml -f docker-compose.dev.yml up --no-deps web mock-api
```

The web app runs as always, with the mock API (`web/mock-api/`, ADR-0033) at its `API_URL`. No api or Postgres is needed. The sign-in link is in the mock's log (`docker compose logs mock-api`). After changing the mock, add `--build`. The part of the email before `@` picks the starting roles: `manager@…` is a manager of the shared Club, `league-manager@…` a League manager there and `admin@…` an Admin. Every sign-in is a new User with no Player and one Club to ask to join. Every primitive is shown in Storybook (see Tests).

## Tests

Integration tests drive the API over HTTP against the real compose Postgres (the project's one testing seam — no mocked repositories):

```sh
docker compose run --rm api-tests
```

Frontend end-to-end tests run Playwright in a container against the production build of the web app, once with the mock API behind it and once with the real API and Postgres. Accessibility checks (axe) run as part of them; Storybook's, below, cover every component in light and dark:

```sh
docker compose --profile e2e run --rm e2e
```

The HTML report lands in `web/playwright-report/`. Pass Playwright arguments after the service name, e.g. `docker compose --profile e2e run --rm e2e npx playwright test --project=mock`. After changing web code, rebuild the images first with `docker compose --profile e2e build`.

The mocked run also leaves DOM snapshots in `web/test-results/chromatic-archives` for visual review in Chromatic (ADR-0015). CI publishes them when the `CHROMATIC_PROJECT_TOKEN` secret is set. To publish a local run:

```sh
CHROMATIC_PROJECT_TOKEN=<token> docker compose --profile e2e run --rm --no-deps chromatic
```

Component stories (Storybook, #46) run in their own compose service. The dev server is at http://localhost:6006:

```sh
docker compose --profile storybook up storybook
```

Build them and check every story's accessibility, in light and dark:

```sh
docker compose --profile storybook run --rm --no-deps storybook sh -c "npm run build-storybook && npm run test-storybook"
```

To publish the built stories to Chromatic's Storybook project, a second project with its own token:

```sh
CHROMATIC_PROJECT_TOKEN=<storybook project token> docker compose --profile storybook run --rm --no-deps chromatic npx chromatic --storybook-build-dir storybook-static --exit-zero-on-changes
```

## Lint and format

CI runs both. ESLint for the web app (`next build` doesn't lint):

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm --no-deps web npm run lint
```

C# formatting and style rules (`api/.editorconfig`). Drop `--verify-no-changes` to apply the fixes:

```sh
docker compose run --rm --no-deps api-tests dotnet format --verify-no-changes
```

The API builds with warnings as errors (`api/Directory.Build.props`), so an analyzer warning fails `api-tests` too.

CI also scans both production images and fails on a critical vulnerability that has a fix (#114). Same command locally, after building the image:

```sh
docker compose build api && scripts/scan-image.sh social-tennis-api
docker compose build web && scripts/scan-image.sh social-tennis-web
```

To accept a finding that can't be fixed yet, add it to `.trivyignore.yaml` with a reason and a review date (the file explains the format).

## Regenerate the TypeScript API client

The typed client (`web/src/lib/api/schema.d.ts`) is generated from the API's OpenAPI document and **committed**. After changing the API surface:

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d api
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm web sh -c "npm ci && npm run api:generate"
```

Drift check (CI runs this): regenerate, then `git diff --exit-code web/src/lib/api/schema.d.ts`.

## Add a database migration

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm api \
  sh -c "dotnet tool restore && dotnet restore src/SocialTennis.Api && dotnet dotnet-ef migrations add <Name> --project src/SocialTennis.Api"
```

The restore comes first: `dotnet-ef` reads the project before building it, and the container's `obj/container` may not be restored yet. Migrations apply on API startup — there is no separate `database update` step.

## Reset the database

```sh
docker compose down -v   # removes the pgdata volume; next start recreates and re-migrates
```

## Run a scheduled job

Scheduled jobs are commands in the API image that run once and exit; in production a Container Apps Job runs each on a cron schedule. Run one by hand against the local database (start the stack first, so migrations are applied):

```sh
docker compose run --rm api job purge-tokens   # deletes used or expired sign-in links and ended sessions
docker compose run --rm api job auto-roll      # closes auto-rolling Stages past their deadline with nothing waiting on the Manager
```

The exit code is 0 when the job finished, 1 when it failed, 2 for an unknown name (the log lists the jobs).

## Hosting (PoC)

The PoC runs on Azure Container Apps with a Neon Postgres database, both scaled to zero when idle (ADR-0019). Terraform in `infra/` creates all of it, run from a toolbox image so nothing is installed on the host.

One-time setup, for the steps only a person can do (accounts, state storage, the GitHub identity):

```sh
bash scripts/setup-hosting.sh
```

Then, from the repo root:

```sh
docker compose -f infra/compose.yml run --rm infra sh ./init.sh
read -rs NEON_API_KEY && read -rs CLOUDFLARE_API_TOKEN   # paste each; nothing is echoed
NEON_API_KEY="$NEON_API_KEY" CLOUDFLARE_API_TOKEN="$CLOUDFLARE_API_TOKEN" \
  docker compose -f infra/compose.yml run --rm infra terraform plan -var image_tag=<commit sha>
```

The two API credentials live only in the `production-plan` and `production` environments' secrets, never in `infra/.env`, so a local plan takes them from your shell for that one command.

The privacy notice's contact address is required (#51): the repository **variable** (not secret) `PRIVACY_CONTACT_EMAIL` (ADR-0024), and `TF_VAR_privacy_contact_email` in `infra/.env` for local plans. The wizard sets both. On a setup from before that, run `gh variable set PRIVACY_CONTACT_EMAIL --body <address>` and add `TF_VAR_privacy_contact_email=<address>` to `infra/.env`. Without it, the plan stops with a message saying so.

A setup from before #113 also put `NEON_API_KEY`, `CLOUDFLARE_API_TOKEN`, `ALERT_EMAIL` and `ADMIN_EMAILS` in the repository's secrets, where any branch's workflow can read them. Once the environments have them, delete those: `gh secret delete <NAME>` for each. The wizards warn about any that are left.

The Terraform state's storage account accepts only Entra ID sign-in, with blob versioning and 30-day soft delete. The wizard sets that up on a new account; for one created before it did, run once:

```sh
docker compose -f infra/compose.yml run --rm infra sh -c \
  'az storage account update -n "$TFSTATE_STORAGE_ACCOUNT" -g "$TFSTATE_RESOURCE_GROUP" --allow-shared-key-access false &&
   az storage account blob-service-properties update --account-name "$TFSTATE_STORAGE_ACCOUNT" -g "$TFSTATE_RESOURCE_GROUP" \
     --enable-versioning true --enable-delete-retention true --delete-retention-days 30 \
     --enable-container-delete-retention true --container-delete-retention-days 30'
```

Releases normally go through GitHub: a merge to `main` that changes `web/` or `api/` publishes images, and **Actions → deploy → Run workflow** plans, waits for approval, applies, and checks the site. It deploys the newest published images unless you give it a commit SHA; to roll back, give it an earlier published one.

By hand, `terraform apply` with the same arguments deploys that image tag. `-var always_on=true` keeps one replica of each app running, which removes cold starts and leaves the free grant. `-var long_cooldown=true` is the cheaper half-step: the apps stay up for 30 minutes after the last request instead of 5. Both are tick boxes on the deploy workflow, and both apply to that deploy only: the next deploy without them puts the default back.

### The club's domain: address and email

The app is served at `https://hitsandgiggles.co.uk` with a certificate Azure manages (ADR-0021), and sign-in links are emailed from that domain through Azure Communication Services (ADR-0020). The domain's DNS is in Cloudflare. Terraform creates the DNS records, the mail resources and the certificate; one more wizard covers what it can't (the Cloudflare token, having Azure verify the domain for email, binding the certificate), between two deploys:

```sh
bash scripts/setup-domain.sh
```

Until that has been run to the end, the app answers only at the address Azure assigns, and the API logs each sign-in email instead of sending it. A tester's link is then read from the log:

```sh
docker compose -f infra/compose.yml run --rm infra \
  az containerapp logs show --name api --resource-group social-tennis --tail 50
```

Locally nothing is ever sent: the API's `Email:Provider` defaults to `Log`, so the link is in `docker compose logs api`. The compose file sets `Email__AllowLogProvider: "true"` for that; anywhere else outside Development, the API refuses to start on `Log` without it (#107).

### Finding an error

Outside Development the API logs one JSON object per line (#115). In the Azure portal, open the Log Analytics workspace `social-tennis-logs` → **Logs** → **Queries** → **Social tennis** → **API errors**: errors and crashes in the API and its scheduled jobs, newest first, with the request path and trace id when the error happened during a request. Logs are kept for 30 days.

### Backups

A job dumps the database every night at 02:30 UTC, and the deploy workflow runs it again before each deploy, which stops if the backup fails (#137). The dumps are kept for 14 days in their own storage account, outside Neon. Neon's own 6 hours of point-in-time history is unaffected. To deploy without the backup, tick **skip_backup** on "Run workflow".

**Once, before the first deploy that creates the job,** on a subscription set up before the wizard registered it: register the managed-identity provider, which the job's identity needs. Without it the apply stops with `MissingSubscriptionRegistration` for `Microsoft.ManagedIdentity`.

```sh
docker compose -f infra/compose.yml run --rm infra sh -c 'sh ./init.sh >/dev/null &&
  az provider register --namespace Microsoft.ManagedIdentity --wait'
```

**Once, after the first deploy that creates the job:** let it write its dumps. The deploy identity can't grant roles, so the owner runs:

```sh
docker compose -f infra/compose.yml run --rm infra sh -c 'sh ./init.sh >/dev/null &&
  az role assignment create --assignee-object-id "$(terraform output -raw backup_principal_id)" \
    --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" \
    --scope "$(terraform output -raw backup_container_id)"'
```

Until then the job runs but fails at the upload, and so does the backup step of a deploy: use **skip_backup** for that deploy.

Restoring, from Neon's history or from a dump: [docs/runbooks/restore-database.md](docs/runbooks/restore-database.md).

## License

GPLv3
