#!/bin/sh
# terraform init against the remote state. The storage account's name is
# globally unique and so differs per owner; it comes from the environment
# rather than being committed.
#
# Run it as `sh ./init.sh`. The repo is edited on Windows, where git doesn't
# record the executable bit, and a bind mount from Windows may not carry it.
set -eu
: "${TFSTATE_RESOURCE_GROUP:?set TFSTATE_RESOURCE_GROUP (scripts/setup-hosting.sh writes infra/.env)}"
: "${TFSTATE_STORAGE_ACCOUNT:?set TFSTATE_STORAGE_ACCOUNT (scripts/setup-hosting.sh writes infra/.env)}"
#
# -lockfile=readonly: providers are installed only if they match the checksums
# in .terraform.lock.hcl, and the file is never rewritten (#111). To change a
# provider version, see "Updating Terraform providers" in docs/architecture.md.
exec terraform init \
  -lockfile=readonly \
  -backend-config="resource_group_name=${TFSTATE_RESOURCE_GROUP}" \
  -backend-config="storage_account_name=${TFSTATE_STORAGE_ACCOUNT}" \
  "$@"
