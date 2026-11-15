# Restore the database

Two ways back, depending on when the damage happened:

| When | Use | Keeps |
|---|---|---|
| Within the last 6 hours | **Neon's point-in-time restore** | Every change up to the moment you pick |
| Older than that, or Neon itself is the problem | **A dump** from the backup storage (#137) | The database as it was at the dump: nightly at 02:30 UTC, and before each deploy |

Either way, **restore into a new Neon branch first**, check it, and only then point the app at it. Never restore over the production branch in place: if the restore is wrong, there is nothing left to go back to.

Commands below run in the infra toolbox, signed in to Azure, unless they say otherwise: `docker compose -f infra/compose.yml run --rm infra sh`, then `sh ./init.sh` so `terraform output` can read the state.

## A. Neon point-in-time restore (last 6 hours)

1. Neon console → the project → **Branches** → **Create branch**. Parent: `production`. Choose **Past data**, and set the time just before the damage.
2. Connect to the new branch (its connection string is on the branch's page) and check the data is right: see "Check the restored data" below.
3. Point the app at it: see "Switch the app over" below.

## B. Restore from a dump

### 1. Find and download the dump

Dumps are in the storage account named by `terraform output -raw backup_storage_account`, container `backups`, under `dumps/`, named by their UTC time, for example `dumps/2026-11-16T0230Z.dump`. They are kept for 14 days. A deleted one can be recovered for 7 days after that from the portal (container → **Show deleted blobs**).

The account accepts Entra ID sign-in only. Owner and Contributor roles don't include reading blobs, so first grant yourself read access, once:

```sh
az role assignment create --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --role "Storage Blob Data Reader" --scope "$(terraform output -raw backup_container_id)"
```

Role assignments take a minute or two to apply. Then list the dumps and download one:

```sh
ACCOUNT="$(terraform output -raw backup_storage_account)"
az storage blob list --auth-mode login --account-name "$ACCOUNT" --container-name backups \
  --prefix dumps/ --query "[].{name:name, size:properties.contentLength}" -o table
az storage blob download --auth-mode login --account-name "$ACCOUNT" --container-name backups \
  --name dumps/<name>.dump --file /tmp/restore.dump
```

### 2. Make an empty Neon branch to restore into

Neon console → **Branches** → **Create branch**, parent `production`, current data. Then empty it, because `pg_restore` puts the schema back as well. On the new branch, in the Neon SQL editor:

```sql
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
```

### 3. Restore

Use the new branch's **direct** (not pooled) connection string as a URI, with `sslmode=verify-full&sslrootcert=system`. From any machine with Docker, in the folder that holds the dump:

```sh
docker run --rm -v "$PWD:/dump" postgres:18-alpine \
  pg_restore --no-owner --no-privileges --exit-on-error \
  --dbname="postgresql://<user>:<password>@<branch host>/tennis?sslmode=verify-full&sslrootcert=system" \
  /dump/restore.dump
```

`--no-owner --no-privileges`: the dump was taken that way, and the branch's role owns what it restores. A restore of the whole database includes `__EFMigrationsHistory`, so the API sees the schema as already migrated and changes nothing on startup.

To restore a single table instead, for example rows deleted by mistake, add `--data-only --table='"Players"'`, and restore into a scratch branch you then copy the rows from.

### 4. Check the restored data

Before switching anything, on the restored branch:

```sql
SELECT (SELECT count(*) FROM "Users") AS users,
       (SELECT count(*) FROM "Players") AS players,
       (SELECT count(*) FROM "Clubs") AS clubs,
       (SELECT max("MigrationId") FROM "__EFMigrationsHistory") AS latest_migration;
```

The counts should be what you expect for the dump's time. The latest migration must not be newer than the one in the code you'll run. If it is, deploy that code first.

## Switch the app over

The app's connection string comes from Terraform (`modules/database`), which always points at the `production` branch. Two ways to switch:

- **Make the restored branch the production branch.** Neon console → the restored branch → **Set as default**, then rename branches so the restored one is `production`. Run a deploy with no other change, so Terraform reads the branch's endpoint again, then check the site.
- **Copy rows back.** For a partial loss, copy the missing rows from the restored branch into `production` with `INSERT … SELECT` over a `dblink` or `postgres_fdw` connection, or `pg_dump --data-only --table=…` from the branch and `pg_restore` into production.

Afterwards, remove your own reader role on the backup container if you no longer need it, and delete scratch branches. Neon's free plan allows only a few.

## Drill

#52's restore drill is this runbook, end to end, into a scratch branch: one point-in-time restore and one dump restore, each checked as in step 4, and the time each took noted here.

| Date | Kind | Time taken | Notes |
|---|---|---|---|
| | | | |
