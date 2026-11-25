# The first Admin comes from configuration, and roles are flags on the User

The app needs someone who can act before anyone has been given a role: create the Club, appoint Managers, approve who joins (#18). The first Admin is therefore **named in configuration**: `Auth:AdminEmails` is a comma-separated list of email addresses, and whoever signs in with one of them is an Admin for as long as it is listed. In production the list is the `ADMIN_EMAILS` GitHub secret, passed through Terraform into a Container Apps secret. There is no seeding step and nothing to do in the database.

Admins granted in the app are **stored on the User** (`User.Roles`, a flags value) and granted or removed by an Admin on the Admin page. The session scheme turns both sources into a role claim on every request, so a change takes effect at once, not at the next sign-in. (Managers were flags here too until ADR-0034, which replaced the Manager flag with a role per Club.)

The app can't be left with no Admin:

- An Admin by configuration can't be demoted in the app, only by changing the configuration. The page says so instead of offering the button.
- A granted Admin can be demoted unless they are the only Admin and no address is configured. Role changes take one database lock, so two Admins demoting each other at the same moment can't both pass that check.

## Consequences

- Losing access to every Admin account is recoverable without touching the database: add an address to `ADMIN_EMAILS` and deploy.
- Whoever can change the deployment's configuration can make themselves an Admin. That is already true of anyone with that access, who could change the database too.
- Removing an address from the list demotes that person only if they were never also granted Admin in the app.
- **Managers are per Club** (ADR-0034): the table beside `User.Roles` this ADR foresaw, `ClubRoles`. It replaced the Manager flag, rather than adding to it as foreseen, so that nothing could go on reading the flag.

## Considered

- **Seed an Admin row in a migration.** It puts a real person's email in the repository, and changing it needs a new migration.
- **The first User to sign in becomes Admin.** No configuration at all, but on a public site that is whoever gets there first.
- **A one-off command run against the database.** It is the "manual database surgery" #18 rules out, and it needs database access from somewhere a person can run it.

## The seeded Club

The walking skeleton seeded one Club, "Social Tennis Club", in a migration. Clubs are now created by an Admin, so the seed is gone and a fresh deployment starts with none. #18 said a migration should delete the seeded row. By the time that migration existed the Club was in production and could have members, and deleting it would have deleted them with it. So the migration deletes the seeded Club **only while nobody is in it, asking to be, or invited**. Where it survives, an Admin renames it.
