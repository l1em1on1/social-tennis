# The database: one Neon project on the free plan.
#
# This module's whole contract is its two outputs. If the Neon provider ever
# becomes a problem, create the project by hand in the Neon console and replace
# this module with one that returns the same outputs from variables.

terraform {
  required_providers {
    neon = {
      source = "kislerdm/neon"
    }
  }
}

variable "name" {
  description = "Project name."
  type        = string
}

variable "neon_org_id" {
  description = "Neon organisation id."
  type        = string
}

variable "neon_region" {
  description = "Neon region id."
  type        = string
}

resource "neon_project" "this" {
  name      = var.name
  org_id    = var.neon_org_id
  region_id = var.neon_region

  # The free plan keeps at most six hours of point-in-time history.
  history_retention_seconds = 21600

  branch {
    name          = "production"
    database_name = "tennis"
    role_name     = "tennis"
  }

  # The smallest compute, never autoscaled, so usage stays inside the free
  # plan's monthly compute-hours. It suspends by itself when idle.
  default_endpoint_settings {
    autoscaling_limit_min_cu = 0.25
    autoscaling_limit_max_cu = 0.25
  }
}

output "connection_string" {
  description = "Npgsql connection string for the API."
  sensitive   = true

  # database_host is the direct endpoint, not the pooler: startup migration
  # holds a session advisory lock, which a pooled connection would break.
  # VerifyFull checks the server certificate and its host name.
  value = join(";", [
    "Host=${neon_project.this.database_host}",
    "Database=${neon_project.this.database_name}",
    "Username=${neon_project.this.database_user}",
    "Password=${neon_project.this.database_password}",
    "SSL Mode=VerifyFull",
  ])
}

output "database_name" {
  description = "Name of the app's database. Also where the migration lock is taken, since a Neon role may not be allowed into the postgres database."
  value       = neon_project.this.database_name
}
