# Sign-in emails are sent by Azure Communication Services, from a domain whose DNS is in Cloudflare

Magic links (ADR-0004) are emailed from `hitsandgiggles.co.uk` through **Azure Communication Services Email**. The domain's DNS is hosted at **Cloudflare**, and Terraform creates both the Azure mail resources and the DNS records (domain ownership, SPF, two DKIM keys, DMARC) in `infra/modules/email`. This lifts the "no real email" limit ADR-0019 accepted while there was no domain.

Azure was chosen because the app already lives in that subscription: no new account, no new vendor key, and the same Terraform and deploy pipeline. It has no free tier, but at about $0.00025 an email a club's sign-ins cost pennies a month, inside the £5 budget alert.

The API sends through one interface, `IEmailSender`, and the provider is picked by the `Email` configuration section. Dev and tests use the `Log` provider and need no mail account. Changing provider is a new class and a configuration change.

## What this accepts

- **Two deploys to switch it on.** Azure will not link a domain it hasn't verified, verification needs the DNS records to exist, and the Terraform provider has no resource that asks Azure to verify. So the first deploy creates the records, `scripts/setup-domain.sh` has Azure check them, and a second deploy (`email_domain_verified = true`) links the domain and configures the API.
- **A connection string, not a managed identity.** The API authenticates to Communication Services with a connection string held as a Container Apps secret. A managed identity would remove the secret, but needs a role assignment, and the deploy identity is a Contributor, which can't grant roles.
- **"Accepted", not "delivered".** The API returns once Azure has accepted the email, so the login form doesn't wait several seconds for delivery. A refusal (bad credentials, unverified domain, throttling) is a 503 the User can retry; a later bounce is not reported back to them.
- **Low sending limits at first.** Azure caps a newly verified domain at a small number of emails per hour and day. That is more than a club needs; raising it is a support request.
- **The sender is `DoNotReply@`.** Azure creates that address on every custom domain. A friendlier sender name is a further resource and can be added later.
- **DMARC starts at `p=none`.** It only reports. Tighten it to `quarantine` once mail is seen to pass SPF and DKIM.

## Considered Options

- **Resend** — free up to 3,000 emails a month and simpler to set up, but a second account and an API key to keep in GitHub, with the domain set up in its dashboard rather than in Terraform.
- **Postmark** — the best transactional deliverability, but 100 free emails a month and then about $15.
- **Sending over SMTP**, so any provider works without code — rejected for Azure, where SMTP sign-in needs an Entra application and its client secret, which is more to manage than the SDK's connection string. The `IEmailSender` seam keeps an SMTP provider a small addition if a later provider wants it.
