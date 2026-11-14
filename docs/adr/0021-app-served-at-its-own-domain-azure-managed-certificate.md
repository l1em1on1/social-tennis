# The app is served at its own domain, with a certificate Azure manages

The app answers at `https://hitsandgiggles.co.uk` instead of only the `*.azurecontainerapps.io` hostname ADR-0019 started with. The hostname is added to the `web` Container App as a custom domain, and the certificate is an **Azure-managed certificate**: free, issued and renewed by Azure. DNS stays at Cloudflare as **DNS-only** records (not proxied), created by Terraform in `infra/modules/domain`.

The emailed sign-in link (ADR-0020) now carries the same domain the email is sent from, which mail filters and people both trust more than a link to an unrelated hostname.

## What this accepts

- **Two deploys and a manual step, shared with email.** Azure issues the certificate only once the hostname is on the app, and the Terraform provider can name a certificate on a custom domain only when creating it, so it can't bind one it had to create afterwards. The first deploy creates the records, the hostname and the certificate; `scripts/setup-domain.sh` binds the certificate and checks https; the second deploy (`app_domain_ready = true`) makes the domain the address used in redirects, cookies and emailed links.
- **Terraform ignores the binding.** The custom domain resource ignores changes to its certificate fields, as the provider's documentation prescribes for managed certificates. If the binding is ever removed by hand, Terraform will not notice; the wizard's bind step puts it back.
- **No Cloudflare proxy.** The A record is DNS-only. Azure ends the TLS connection and validates the certificate over plain http at that address, so Cloudflare's CDN, caching and firewall are not in front of the app. Putting them there later means Cloudflare's "Full (strict)" mode on top of this, not instead of it.
- **An A record to the environment's address**, not a CNAME, because the bare domain can't hold a CNAME. The address belongs to the Container Apps environment and changes only if the environment is recreated, in which case the next apply updates the record.
- **The Azure hostname keeps working.** Nothing redirects it. Sessions don't carry over: a cookie belongs to the host that set it, so the switch signs everyone out once.

## Considered Options

- **Cloudflare in front (proxied), with its certificate** — Cloudflare would end TLS, but Azure routes by hostname and still needs the custom domain and a certificate it accepts, so it adds a layer without removing one.
- **A `www` or `app` subdomain** — allows a CNAME and CNAME validation, but the bare domain is what people will type. A subdomain still works: `app_domain` takes any hostname in the zone.
- **Bringing our own certificate** (Let's Encrypt or a Cloudflare origin certificate) — Terraform could then bind it in one apply, at the price of renewing it ourselves.
