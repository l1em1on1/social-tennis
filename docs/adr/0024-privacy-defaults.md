# Privacy: standard defaults for a small UK club app

The app holds members' email addresses and their Players' names, gender, Level and Rating, including Players a parent holds for their children. We start with the common, minimal setup for a small club app, and change it later if the club needs more (#51, decided 2026-11-13: "start standard, don't overcomplicate").

- **Controller:** Hits & Giggles. The name (`PRIVACY_CONTROLLER`, defaulting to it) and the address for data requests (`PRIVACY_CONTACT_EMAIL`) come from configuration. Production's address is a repository variable the owner sets (not a secret: the notice shows it publicly), and Terraform requires it: the notice must give the controller's contact details.
- **Lawful basis:** legitimate interests, to run the leagues and socials members take part in. No consent banner, and no email but sign-in links.
- **Cookies:** the session and acting-Player cookies only, both strictly necessary. No analytics or tracking.
- **Children:** a child's Player is held by a parent's login, with no email or login of its own. Children don't sign in.
- **Players a Manager adds:** a Manager may add a member by name and email before they sign in. The sign-in email links to the notice.
- **What the notice lists:** the email address, when they last signed in, which Clubs and Leagues they manage and whether they're an Admin; each Player's name, gender, Level and Rating; Club and League memberships, requests to join or leave and their answers (#21), Games and Scores; the days a Player says they're free to play, and a Game's agreed Date and Court note (#9); addresses a Manager pre-approves before the person signs in; and each sign-in link with its address, until the nightly purge (#112).
- **Sign-in requests:** an unsalted SHA-256 of the client's network address is kept with each sign-in link and goes with it in the nightly purge (#108, #112). It is pseudonymous, not anonymous (it could be matched back), and the notice says so.
- **Who sees it:** in the club, Admins see email addresses and roles; Managers see their Clubs' members with email addresses, requests and pre-approved addresses, and no other Club's (ADR-0034); a League manager sees the names of their Club's members, to add them, and the email address, gender and rating of the Players in the Leagues they run or asking to join one (#220); members see Players' names, Games and Scores, and who runs their League by name, never by email. A Player's whole list of free days reaches only that Player. A Game's own Players see the days that suit everyone in it, which shows each of them which of the others' free days they share; nobody else sees them. Its agreed Date and Court note reach whoever can see the Game (#9). The notice's "Who sees it" says both. Outside the club, none, beyond the providers that run the app: Microsoft Azure (hosting, email) and Neon (database).
- **Deletion:** a member emails the contact address, and an Admin deletes the account. The User, sessions and tokens go, and their Players are anonymised as "Former player", so others' results stay intact (#116). Their free days go too (#9).
- **Retention:** an account with no sign-in for 24 months is deleted the same way, by a scheduled job (#117), unless it has never been signed in to (amendment below). Database backups are kept 14 days, plus 7 of soft delete (#137), so the notice says "up to three weeks".

The notice is a public page, `/privacy`, linked from the login page, the app shell and the sign-in email.

## Consequences

- **Deletion is built (#116, 2026-11-24).** An Admin removes the User on Admin (`DELETE /users/{userId}`, `AccountDeletion`), and the retention job uses that path. Their Players keep their places, Games and Scores as "Former player", with Gender and Level cleared, so nobody else's results change. They leave their Clubs. Their sign-in links, invites and sessions go, and the email can sign up again as a new account. Rating, a Manager's seeding number, stays with the Former player. The notice said that retention wasn't in place yet, until #117.
- **The last sign-in is recorded (#117, part 1, 2026-11-24).** Redeeming a sign-in link sets `Users.LastSignedInAt`. A session lasts 30 days and isn't extended by use, so someone who uses the app signs in at least that often, and the last sign-in stands for the last use. It is null for a User a Manager added who hasn't signed in, and for one who hasn't since this was added. Nothing was filled in for existing Users: their next sign-in does it. The job that deletes after 24 months is part 2. No account can be due before November 2028.
- **League managers (#220, 2026-11-25).** What a League manager sees is limited to names, and to the Players in the Leagues they run (ADR-0034). They keep that while they keep a League or the role, which stay until a Club's manager takes them, even if they leave the Club.
- A change to what the app stores, who it shares with, or how long it keeps data updates the notice and this ADR together.

## Considered

- **Consent for everything.** Not needed: running the leagues members joined is the purpose, and nothing optional is collected.
- **A self-service delete button.** Deferred: requests are rare in a club, and an Admin handling them keeps a person in the loop.

**Amended (2026-11-26): the retention job (#117, part 2).** `purge-inactive-accounts` runs daily and deletes, through `AccountDeletion`, every account whose last sign-in is over 24 months ago, whether or not its Players are in a League; their results stay, as Former player's. An account never signed in to stays (owner, 2026-11-26): a Manager can add a member by name and email and record their scores, so a regular player may never sign in.

Users who signed in before the last sign-in was recorded (part 1) have none on record either. The app isn't live yet, and the owner clears the production database at the next release (2026-11-26), so every account there has its sign-ins recorded from the start. No account can be due before November 2028.
