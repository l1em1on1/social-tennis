# v1 auth: email/SMS magic link; OAuth/OIDC as a pluggable future goal

v1 login is passwordless email/SMS magic link. The account model should support adding external OAuth/OIDC providers later (e.g. Google) via ASP.NET Core's `AddOAuth`/`AddOpenIdConnect`, without restructuring identity.

WhatsApp was considered as a login transport and rejected — the underlying need (leagues are currently coordinated over WhatsApp) is a notifications concern, not an auth mechanism, and WhatsApp-as-login would require a Meta/Twilio business integration with no clear benefit over email/SMS.

**Investigated and ruled out for now**: "Login with ClubSpark" via `auth.clubspark.uk`. It has no OpenID Connect discovery document (`/.well-known/openid-configuration` 404s) and the endpoint itself redirects to a plain login page, not an authorize/token API. The LTA/ClubSpark SSO that does exist is a proprietary internal account-linking flow across LTA's own products, not a federated identity provider third parties can register against. No evidence of a private/partner OAuth arrangement was found, but none was ruled out either — revisit only if a direct conversation with ClubSpark/LTA opens up a partner integration path.
