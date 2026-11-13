#!/bin/sh
# terraform init against the remote state. The storage account's name is
# globally unique and so differs per owner; it comes from the environment
# rather than being committed.
set -eu
: "${TFSTATE_RESOURCE_GROUP:?set TFSTATE_RESOURCE_GROUP (scripts/setup-hosting.sh writes infra/.env)}"
: "${TFSTATE_STORAGE_ACCOUNT:?set TFSTATE_STORAGE_ACCOUNT (scripts/setup-hosting.sh writes infra/.env)}"
exec terraform init \
  -backend-config="resource_group_name=${TFSTATE_RESOURCE_GROUP}" \
  -backend-config="storage_account_name=${TFSTATE_STORAGE_ACCOUNT}" \
  "$@"
