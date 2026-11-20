---
status: accepted
---

# Releases: images published on merge, deployed by an approved Terraform run, pinned by digest

The release flow (`deploy.yml`, #27, #104, #111, #113, #137) is described in `docs/architecture.md`, "Release flow". This records why it works that way.

- **One mechanism.** A release is a `terraform apply` with different images, so infrastructure changes and releases take the same path, and a rollback is the same run with an earlier commit.
- **Publishing and deploying are separate.** A merge that changes `web/` or `api/` publishes images tagged with its commit. Nothing is deployed until someone runs the workflow and a reviewer approves the `production` environment.
- **The newest published images, by default.** Images are built only for commits that change an app. The commits after the newest one with images changed neither app, so those images hold the same code.
- **Only code that reached `main`.** The plan job refuses a commit that isn't on `main`, even if images were published for it.
- **Images by digest.** The plan resolves each tag to its digest, and the apply job gets the same digests. It then runs exactly the images that were planned, even if a tag is pushed again in between.
- **The plan isn't uploaded.** A saved plan file contains the database password, and a public repository's artifacts can be downloaded. So the apply job plans again.
- **Credentials only where a deploy runs.** Azure trusts a short-lived OIDC token, and only for the `production-plan` and `production` environments, which both deploy from `main` only. No cloud key is stored in GitHub. The Neon and Cloudflare credentials, the alert email and the Admin addresses are secrets of those environments, not of the repository (#104, #113), so only the deploy jobs can read them. Against the infrastructure, a pull request runs only `infra.yml`'s `fmt` and `validate`, with no cloud credentials, so it never holds one that can change infrastructure.
- **Back up, then apply** (#137). A failed backup means nothing is deployed.
