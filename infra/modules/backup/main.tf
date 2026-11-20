# Database backups (#137): a dump of the database every night, and before each
# deploy, kept for a fortnight in a storage account outside Neon.
#
# Neon's own point-in-time history (6 hours on the free plan, modules/database)
# is unaffected. These dumps are a second, longer-lived copy that survives a
# problem noticed days later, or a problem with Neon itself.
#
# The job is two public images pinned by digest, nothing built here: an init
# container runs pg_dump into a scratch volume, then the Azure CLI uploads the
# file with the job's managed identity. Storage accepts Entra ID sign-in only,
# so there is no key anywhere.
#
# One step is the owner's (README, "Backups"): granting the identity write
# access to the container. The deploy identity is Contributor, which can create
# everything here but can't grant roles. Until then the job runs and fails at
# the upload.
#
# Dependabot can't see images named in Terraform: bump both by hand, digest
# included (docs/architecture.md, "Versions").

variable "name" {
  description = "Prefix for resource names."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group to create everything in."
  type        = string
}

variable "resource_group_id" {
  description = "Id of that resource group, which makes the storage account's name unique."
  type        = string
}

variable "container_app_environment_id" {
  description = "The Container Apps environment the job runs in."
  type        = string
}

variable "database_uri" {
  description = "libpq connection URI for pg_dump."
  type        = string
  sensitive   = true
}

variable "retention_days" {
  description = "How long a dump is kept."
  type        = number
  default     = 14
}

locals {
  # pg_dump's major version must be at least the server's (Neon's default).
  postgres_image  = "postgres:18-alpine@sha256:b6972e1115a4ff8eba5d7f7d19184926d1e16d7d1136ae1e1a7ebf286da0f015"
  azure_cli_image = "mcr.microsoft.com/azure-cli:2.92.0@sha256:a4e2660b4099b76f02001a9316a0eb0dc56e330b5f7d1d1312c932200645bf26"
  container_name  = "backups"
}

# Storage account names are global, lowercase letters and digits, 3–24 long.
resource "azurerm_storage_account" "backups" {
  name                     = "${replace(var.name, "-", "")}bk${substr(sha1(var.resource_group_id), 0, 6)}"
  location                 = var.location
  resource_group_name      = var.resource_group_name
  account_tier             = "Standard"
  account_replication_type = "LRS"

  # Entra ID only: no account key, no SAS, nothing public.
  shared_access_key_enabled         = false
  default_to_oauth_authentication   = true
  allow_nested_items_to_be_public   = false
  cross_tenant_replication_enabled  = false
  min_tls_version                   = "TLS1_2"
  https_traffic_only_enabled        = true
  infrastructure_encryption_enabled = true

  # A deleted dump can be brought back for a week.
  blob_properties {
    delete_retention_policy {
      days = 7
    }
    container_delete_retention_policy {
      days = 7
    }
  }
}

# Created through Azure Resource Manager (storage_account_id), so Terraform
# needs no data-plane access, which the account's Entra-only setting would
# otherwise require (versions.tf: data_plane_available = false).
resource "azurerm_storage_container" "backups" {
  name                  = local.container_name
  storage_account_id    = azurerm_storage_account.backups.id
  container_access_type = "private"
}

resource "azurerm_storage_management_policy" "backups" {
  storage_account_id = azurerm_storage_account.backups.id

  rule {
    name    = "expire-dumps"
    enabled = true
    filters {
      blob_types   = ["blockBlob"]
      prefix_match = ["${local.container_name}/dumps/"]
    }
    actions {
      base_blob {
        delete_after_days_since_creation_greater_than = var.retention_days
      }
    }
  }
}

# The job's own identity: it can write dumps once the owner grants it Storage
# Blob Data Contributor on the container, and nothing else.
resource "azurerm_user_assigned_identity" "backup" {
  name                = "${var.name}-backup"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_container_app_job" "backup" {
  name                         = "job-backup-database"
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  # The environment's Consumption profile, which Azure assigns anyway (#150).
  workload_profile_name = "Consumption"

  # Pulling the Azure CLI image takes a while on a cold node; a dump of this
  # database takes seconds. A failed run is retried once.
  replica_timeout_in_seconds = 900
  replica_retry_limit        = 1

  # 02:30 UTC, before the token purge at 03:00, so a dump never misses a row
  # the purge was about to delete. The deploy workflow also starts it.
  schedule_trigger_config {
    cron_expression = "30 2 * * *"
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.backup.id]
  }

  secret {
    name  = "database-uri"
    value = var.database_uri
  }

  template {
    volume {
      name         = "dump"
      storage_type = "EmptyDir"
    }

    # pg_dump's custom format: compressed, and pg_restore can restore all of
    # it or single tables. No owners or grants: a restore goes into a new Neon
    # branch whose role differs.
    init_container {
      name   = "dump"
      image  = local.postgres_image
      cpu    = 0.25
      memory = "0.5Gi"
      command = [
        "sh", "-c",
        "pg_dump --format=custom --no-owner --no-privileges --file=/dump/db.dump \"$DATABASE_URI\" && ls -l /dump/db.dump",
      ]
      env {
        name        = "DATABASE_URI"
        secret_name = "database-uri"
      }
      volume_mounts {
        name = "dump"
        path = "/dump"
      }
    }

    container {
      name   = "upload"
      image  = local.azure_cli_image
      cpu    = 0.25
      memory = "0.5Gi"
      command = [
        "sh", "-c",
        join(" && ", [
          "az login --identity --client-id \"$AZURE_CLIENT_ID\" --output none",
          "name=\"dumps/$(date -u +%Y-%m-%dT%H%MZ).dump\"",
          "az storage blob upload --auth-mode login --account-name \"$STORAGE_ACCOUNT\" --container-name \"$CONTAINER\" --name \"$name\" --file /dump/db.dump --no-progress --output none",
          "echo \"Uploaded $name\"",
        ]),
      ]
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.backup.client_id
      }
      env {
        name  = "STORAGE_ACCOUNT"
        value = azurerm_storage_account.backups.name
      }
      env {
        name  = "CONTAINER"
        value = local.container_name
      }
      volume_mounts {
        name = "dump"
        path = "/dump"
      }
    }
  }
}

output "job_name" {
  description = "The backup job, which the deploy workflow starts before applying."
  value       = azurerm_container_app_job.backup.name
}

output "storage_account_name" {
  description = "Where the dumps are, under backups/dumps/."
  value       = azurerm_storage_account.backups.name
}

output "container_id" {
  description = "Resource id of the backups container: the scope for the job's role, and for anyone downloading a dump."
  value       = azurerm_storage_container.backups.id
}

output "principal_id" {
  description = "Object id of the job's identity, which needs Storage Blob Data Contributor on the container."
  value       = azurerm_user_assigned_identity.backup.principal_id
}
