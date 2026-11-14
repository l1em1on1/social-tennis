# The compute: a Container Apps environment with the two apps in it.
#
# Both apps scale to zero unless always_on is set. Neither runs more than one
# replica: the API applies migrations on startup, which the architecture doc
# scopes to a single instance.

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

variable "web_image" {
  description = "Full image reference for web."
  type        = string
}

variable "api_image" {
  description = "Full image reference for api."
  type        = string
}

variable "always_on" {
  description = "Keep one replica of each app running."
  type        = bool
}

variable "database_connection_string" {
  description = "Npgsql connection string."
  type        = string
  sensitive   = true
}

variable "database_name" {
  description = "Database that holds the startup migration's advisory lock."
  type        = string
}

locals {
  min_replicas = var.always_on ? 1 : 0

  # An app with external ingress is served at <app>.<environment domain>.
  # Building the address from the environment, not from the web app's own
  # attributes, lets the web app be told its own public address.
  web_url = "https://web.${azurerm_container_app_environment.this.default_domain}"
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name}-logs"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_container_app_environment" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  # Said explicitly: azurerm 5 no longer infers the destination from the
  # workspace id, and rejects the id without it.
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
}

resource "azurerm_container_app" "api" {
  name                         = "api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  secret {
    name  = "database-connection-string"
    value = var.database_connection_string
  }

  # Internal only: reachable from web as http://api, never from the internet
  # (ADR-0001). Plain http is allowed because the caller is inside the
  # environment and addresses the app by name.
  ingress {
    external_enabled           = false
    allow_insecure_connections = true
    target_port                = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = local.min_replicas
    max_replicas = 1

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name        = "ConnectionStrings__Default"
        secret_name = "database-connection-string"
      }
      env {
        name  = "Database__MigrationLockDatabase"
        value = var.database_name
      }
      env {
        name  = "Auth__VerifyUrlBase"
        value = "${local.web_url}/auth/verify"
      }

      # A cold start wakes the database and checks migrations before the API
      # listens, so give it up to two minutes before calling it failed.
      startup_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health"
        interval_seconds        = 5
        failure_count_threshold = 24
      }

      # /health runs no checks. A probe that queried Postgres would keep Neon
      # awake and spend its compute allowance.
      liveness_probe {
        transport        = "HTTP"
        port             = 8080
        path             = "/health"
        interval_seconds = 30
      }
    }
  }
}

resource "azurerm_container_app" "web" {
  name                         = "web"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  ingress {
    external_enabled = true
    target_port      = 3000

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = local.min_replicas
    max_replicas = 1

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "API_URL"
        value = "http://${azurerm_container_app.api.name}"
      }
      # https, so the session cookies are Secure (web/src/lib/session.ts).
      env {
        name  = "PUBLIC_BASE_URL"
        value = local.web_url
      }

      # /login renders without calling the API, so probing it wakes nothing.
      liveness_probe {
        transport        = "HTTP"
        port             = 3000
        path             = "/login"
        interval_seconds = 30
      }
    }
  }
}

output "web_url" {
  description = "Public address of the app."
  value       = local.web_url
}

output "api_app_name" {
  description = "Name of the api Container App."
  value       = azurerm_container_app.api.name
}
