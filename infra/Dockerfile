# The infrastructure toolbox: Terraform plus the Azure CLI, so neither is ever
# installed on the host (ADR-0005). Terraform's azurerm provider signs in
# through the `az` login held in this image's volume, which is why the two
# live in one image.
#
# Versions are pinned; Dependabot's docker ecosystem watches this file.
FROM hashicorp/terraform:1.17.2 AS terraform

FROM mcr.microsoft.com/azure-cli:2.93.0
# `az communication email …`, which scripts/setup-domain.sh uses to have Azure
# verify the sending domain (ADR-0020). Not part of the core CLI. Pinned (#113),
# but Dependabot can't watch a CLI extension: bump it by hand, from the VERSION
# in Azure/azure-cli-extensions src/communication/setup.py.
RUN az extension add --name communication --version 1.14.0
COPY --from=terraform /bin/terraform /usr/local/bin/terraform
WORKDIR /infra
