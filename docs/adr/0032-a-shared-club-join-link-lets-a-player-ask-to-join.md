---
status: accepted
---

# A shared Club join link lets a Player ask to join

Until now, Home listed every Club, and a Player asked to join one from there. A Manager approved or declined, or let people in directly: by adding a Player by name and email, or by pre-approving an email. The owner (2026-11-23, #222) wants Home without the Clubs list, since most people belong to one Club. People will mostly be added by email, and a link can bring in the rest. Three answers settled it:

- Opening the link lets a Player **ask**; a Manager still approves.
- There is **one shared link per Club**, which works until a Manager resets it or turns it off.
- The request-and-approve flow stays, **reached only through the link**.

## Decision

- **The link is a code on the Club** (`Clubs.JoinCode`): 128 random bits in base64url, 22 characters, unique. Opening `/join/{code}` on the web app shows the Club's name and an "Ask to join" button. Opening the page changes nothing: chat apps fetch links for their previews, so only the button's POST asks, as on the sign-in page (#19).
- **A Manager controls it on Manage.**
  - **Turn on** (`PUT /clubs/{clubId}/join-link`) keeps a link that's already on, so two Managers, or a double press, get one working link.
  - **Reset** (`POST …/join-link/reset`) makes a new code, and the old link stops working.
  - **Turn off** (`DELETE …/join-link`) clears the code.
  - Every Club's link starts off.
  - The code is stored as it is, not hashed. Managers need to see it again, and it lets someone only ask, never get in.
- **Asking needs the current code** (`PUT /join-links/{code}/members/{playerId}`), and asking by Club id is gone. Without that, turning the link off or resetting it would change nothing. The ask itself is unchanged (CONTEXT.md, Club): it waits for a Manager, unless the User is in the Club already or a Manager pre-approved their email.
- **A link that's off, was reset, or never existed** answers the same 404, and the page says only that the link doesn't work any more.
- **Players don't list Clubs:** `GET /clubs` is for Managers and Admins.
- **Signing in from a link returns to it on any device.** The proxy sends a signed-out visitor to `/login?join={code}`; a visitor whose session has ended gets there the same way, through the page's 401. The sign-in request carries the code (`MagicLinkRequest.Join`, its shape checked), and the API puts it in the emailed link as `&join=`. The verify page then redirects to `/join/{code}` once signed in. Only a well-formed code travels, never a path, so the redirect can't be steered elsewhere.

## Considered Options

- **The link lets people in at once.** The owner chose approval: a link forwarded beyond the club only lets someone ask.
- **A link per person, used once.** That is close to pre-approving an email, which already exists. A shared link is what a Manager can post in the club's chat.
- **A link that also expires.** A Manager can reset or turn it off whenever it has gone too far, so an expiry would only make Managers renew it.
- **A cookie to remember the link through sign-in.** This was rejected: the emailed link often opens in another browser, such as the mail app's. The code rides in the link instead.
- **Keep asking by Club id, with the link as a convenience.** This was rejected, because turning the link off would then stop nothing.

## Consequences

- **Every Club's link starts off.** A Manager turns it on, once, to share it; the owner's action list (#120) says so.
- **Requests made through a link outlive it.** Resetting or turning it off stops new requests; those already waiting stay for a Manager to decide.
- **The test suites ask through the link:** the integration tests' `AskToJoinAsync`, and the e2e `sharedJoinLink`, which turns the shared Club's link on and never resets it.
