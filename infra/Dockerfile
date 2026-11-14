# The infrastructure toolbox: Terraform plus the Azure CLI, so neither is ever
# installed on the host (ADR-0005). Terraform's azurerm provider signs in
# through the `az` login held in this image's volume, which is why the two
# live in one image.
#
# Versions are pinned; Dependabot's docker ecosystem watches this file.
FROM hashicorp/terraform:1.17.2 AS terraform

FROM mcr.microsoft.com/azure-cli:2.92.0
# `az communication email …`, which scripts/setup-domain.sh uses to have Azure
# verify the sending domain (ADR-0020). Not part of the core CLI, and the one
# thing here that isn't pinned: Dependabot can't watch a CLI extension.
RUN az extension add --name communication
COPY --from=terraform /bin/terraform /usr/local/bin/terraform
WORKDIR /infra
