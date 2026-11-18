# Privacy: standard defaults for a small UK club app

The app holds members' email addresses and their Players' names, gender, Level and Rating, including Players a parent holds for their children. We start with the common, minimal setup for a small club app, and change it later if the club needs more (#51, decided 2026-11-13: "start standard, don't overcomplicate").

- **Controller:** Hits & Giggles. The name (`PRIVACY_CONTROLLER`, defaulting to it) and the address for data requests (`PRIVACY_CONTACT_EMAIL`) come from configuration. Production's address is a repository variable the owner sets.
- **Lawful basis:** legitimate interests, to run the leagues and socials members take part in. No consent banner, and no email but sign-in links.
- **Cookies:** the session and acting-Player cookies only, both strictly necessary. No analytics or tracking.
- **Children:** a child's Player is held by a parent's login, with no email or login of its own. Children don't sign in.
- **Players a Manager adds:** a Manager may add a member by name and email before they sign in. The sign-in email links to the notice.
- **What the notice lists:** the email address and roles; each Player's name, gender, Level and Rating; Club and League memberships, requests and their answers, Games and Scores; addresses a Manager pre-approves before the person signs in; and each sign-in link with its address, until the nightly purge (#112).
- **Sign-in requests:** an unsalted SHA-256 of the client's network address is kept with each sign-in link and goes with it in the nightly purge (#108, #112). It is pseudonymous, not anonymous (it could be matched back), and the notice says so.
- **Sharing:** none, beyond the providers that run the app: Microsoft Azure (hosting, email) and Neon (database).
- **Deletion:** a member emails the contact address, and an Admin deletes the account. The User, sessions and tokens go, and their Players are anonymised as "Former player", so others' results stay intact (#116).
- **Retention:** an account with no sign-in for 24 months is deleted the same way, by a scheduled job (#117). Database backups are kept 14 days, plus 7 of soft delete (#137), so the notice says "up to three weeks".

The notice is a public page, `/privacy`, linked from the login page, the app shell and the sign-in email.

## Consequences

- Until #116 is built, deletion is done by hand, following the same rule. Retention can't start until #117 records when a User last signed in, and the notice says that it isn't in place yet.
- A change to what the app stores, who it shares with, or how long it keeps data updates the notice and this ADR together.

## Considered

- **Consent for everything.** Not needed: running the leagues members joined is the purpose, and nothing optional is collected.
- **A self-service delete button.** Deferred: requests are rare in a club, and an Admin handling them keeps a person in the loop.
