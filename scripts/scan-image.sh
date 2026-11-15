#!/usr/bin/env sh
# Scans a locally built image for vulnerabilities and fails on a critical one
# that has a fix (#114). CI runs it on the api and web images it builds, and it
# runs the same way locally:
#
#   docker compose build api && scripts/scan-image.sh social-tennis-api
#
# Trivy runs from its official image, pinned by digest: its own release
# channels were compromised once (GHSA-69fq-xp46-6x23, March 2026), so a tag
# alone isn't trusted. Dependabot can't see an image named in a script: bump it
# by hand, digest included (docs/architecture.md, "Versions").
#
# Accepted findings are in .trivyignore.yaml at the repo root, each with the
# reason and an expiry date. Trivy stops honouring an entry once it has
# expired, so an accepted finding fails CI again on its review date.
set -eu

image="${1:?usage: scripts/scan-image.sh <image>}"
trivy="aquasec/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb"
repo="$(cd "$(dirname "$0")/.." && pwd)"

# The Docker socket lets Trivy read the image from the local daemon, so
# nothing is pushed. The vulnerability database is cached in a named volume.
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock:ro \
  -v trivy-cache:/root/.cache/trivy \
  -v "$repo/.trivyignore.yaml:/trivyignore.yaml:ro" \
  "$trivy" image \
  --scanners vuln \
  --severity CRITICAL \
  --ignore-unfixed \
  --ignorefile /trivyignore.yaml \
  --exit-code 1 \
  --no-progress \
  "$image"
