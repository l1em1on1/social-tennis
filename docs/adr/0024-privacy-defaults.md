# Privacy: standard defaults for a small UK club app

The app holds members' email addresses and their Players' names, gender, Level and Rating, including Players a parent holds for their children. We start with the common, minimal setup for a small club app, and change it later if the club needs more (#51, decided 2026-11-13: "start standard, don't overcomplicate").

- **Controller:** Hits & Giggles. The name (`PRIVACY_CONTROLLER`, defaulting to it) and the address for data requests (`PRIVACY_CONTACT_EMAIL`) come from configuration. Production's address is a repository variable the owner sets.
- **Lawful basis:** legitimate interests, to run the leagues and socials members take part in. No consent banner, and no email but sign-in links.
- **Cookies:** the session and acting-Player cookies only, both strictly necessary. No analytics or tracking.
- **Children:** a child's Player is held by a parent's login. Children don't sign in, and a child's Player holds only name, gender, Level and Rating.
- **Players a Manager adds:** a Manager may add a member by name and email before they sign in. The sign-in email links to the notice.
- **Sign-in requests:** a hash of the client's network address is kept with each sign-in link and goes with it, within a day of the link being used or expiring (#108, #112).
- **Sharing:** none, beyond the providers that run the app: Microsoft Azure (hosting, email) and Neon (database).
- **Deletion:** a member emails the contact address, and an Admin deletes the account. The User, sessions and tokens go, and their Players are anonymised as "Former player", so others' results stay intact (#116).
- **Retention:** an account with no sign-in for 24 months is deleted the same way, by a scheduled job (#117). Database backups are kept 14 days (#137).

The notice is a public page, `/privacy`, linked from the login page, the app shell and the sign-in email.

## Consequences

- Until #116 and #117 are built, an Admin carries out deletion and retention by hand. The notice states the policy, not the mechanism.
- A change to what the app stores, who it shares with, or how long it keeps data updates the notice and this ADR together.

## Considered

- **Consent for everything.** Not needed: running the leagues members joined is the purpose, and nothing optional is collected.
- **A self-service delete button.** Deferred: requests are rare in a club, and an Admin handling them keeps a person in the loop.
