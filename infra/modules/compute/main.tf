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

variable "long_cooldown" {
  description = "Keep each app running for 30 minutes after its last request, not 5."
  type        = bool
}

variable "admin_emails" {
  description = "Email addresses that are Admins, separated by commas. May be empty."
  type        = string
  sensitive   = true
}

variable "privacy_contact_email" {
  description = "Data-request address shown on the privacy notice. May be empty."
  type        = string
  default     = ""
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

variable "public_url" {
  description = "Address the app is served at, when it isn't the one Azure assigns. Null: Azure's."
  type        = string
  nullable    = true
}

variable "email_enabled" {
  description = "Send sign-in emails through Azure Communication Services. False: the API logs them."
  type        = bool
}

variable "email_connection_string" {
  description = "Communication Services connection string. Null unless email_enabled."
  type        = string
  sensitive   = true
  nullable    = true
}

variable "email_from_address" {
  description = "Sender address on the verified domain. Null unless email_enabled."
  type        = string
  nullable    = true
}

locals {
  min_replicas = var.always_on ? 1 : 0

  # How long an app stays up after its last request before scaling to zero.
  # 300 is Azure's default. Both apps together use 0.5 vCPU and 1 GiB, so the
  # free grant covers about 100 hours of running a month: at 1800, roughly six
  # separate visits a day. No effect when always_on is set.
  cooldown_seconds = var.long_cooldown ? 1800 : 300

  # An app with external ingress is served at <app>.<environment domain>.
  # Building the address from the environment, not from the web app's own
  # attributes, lets the web app be told its own public address.
  default_web_url = "https://web.${azurerm_container_app_environment.this.default_domain}"

  # What redirects, cookies and the emailed link are built from.
  web_url = coalesce(var.public_url, local.default_web_url)
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name}-logs"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

# Errors and crashes in the API and its scheduled jobs, newest first, with the
# request path and trace id when the error happened inside a request (#115).
# The API logs one JSON object per line outside Development; Container Apps
# puts each line in Log_s. Every error row is kept: the request fields are read
# out of the scopes text, so a row with no scopes (startup, a job) still shows.
# A crash before logging exists is plain text from the .NET runtime, starting
# "Unhandled exception". Job rows are matched by name: the job column is read
# with column_ifexists, so the query works whether or not the table has it.
# In the portal: the workspace → Logs → Queries → "Social tennis" → "API errors".
resource "azurerm_log_analytics_saved_search" "api_errors" {
  name                       = "api-errors"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  category                   = "Social tennis"
  display_name               = "API errors"
  query                      = <<-KQL
    ContainerAppConsoleLogs_CL
    | extend Source = coalesce(tostring(column_ifexists("ContainerJobName_s", "")), ContainerAppName_s)
    | where Source == "api" or Source startswith "job-"
    | extend entry = parse_json(Log_s)
    | extend Level = tostring(entry.LogLevel)
    | where Level in ("Error", "Critical") or (isempty(Level) and Log_s startswith "Unhandled exception")
    | extend scopes = tostring(entry.Scopes)
    | project TimeGenerated, Source, Level,
        Category = tostring(entry.Category),
        Message = coalesce(tostring(entry.Message), Log_s),
        Exception = tostring(entry.Exception),
        RequestPath = extract(@'"RequestPath":"([^"]*)"', 1, scopes),
        TraceId = extract(@'"TraceId":"([^"]*)"', 1, scopes)
    | order by TimeGenerated desc
  KQL
}

resource "azurerm_container_app_environment" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  # Said explicitly: azurerm 5 no longer infers the destination from the
  # workspace id, and rejects the id without it.
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  # Azure gives the environment this profile, and puts every app and job on
  # it, whether or not it is declared. Declared, so a plan with nothing to do
  # reads "No changes" instead of removing it each time (#150).
  workload_profile {
    name                  = "Consumption"
    workload_profile_type = "Consumption"
  }
}

resource "azurerm_container_app" "api" {
  name                         = "api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  secret {
    name  = "database-connection-string"
    value = var.database_connection_string
  }

  # A secret, so the addresses stay out of the plan the deploy prints. A
  # secret can't be empty, and "none" is no address, so it names nobody.
  secret {
    name  = "admin-emails"
    value = var.admin_emails != "" ? var.admin_emails : "none"
  }

  # Keyed on email_enabled, not on the connection string: a sensitive value
  # can't decide whether a block exists.
  dynamic "secret" {
    for_each = var.email_enabled ? [1] : []
    content {
      name  = "email-connection-string"
      value = var.email_connection_string
    }
  }

  # Internal only: reachable from other apps in the environment, never from
  # the internet (ADR-0001). https only (#111): web calls the internal FQDN
  # over TLS, and a plain-http request is redirected to https.
  ingress {
    external_enabled           = false
    allow_insecure_connections = false
    target_port                = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = local.min_replicas
    max_replicas = 1

    cooldown_period_in_seconds = local.cooldown_seconds

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
      # Who is an Admin (and so can approve club members) on a fresh deployment.
      env {
        name        = "Auth__AdminEmails"
        secret_name = "admin-emails"
      }
      env {
        name  = "Auth__VerifyUrlBase"
        value = "${local.web_url}/auth/verify"
      }

      # Without these the API's Email:Provider stays "Log" (ADR-0020). Outside
      # Development the API refuses to start on "Log" unless it is allowed
      # explicitly (#107), which only the no-email path does: there, sign-in
      # links are read from the API's logs on purpose.
      dynamic "env" {
        for_each = var.email_enabled ? {
          Email__Provider    = "AzureCommunicationServices"
          Email__FromAddress = var.email_from_address
          } : {
          Email__AllowLogProvider = "true"
        }
        content {
          name  = env.key
          value = env.value
        }
      }
      dynamic "env" {
        for_each = var.email_enabled ? [1] : []
        content {
          name        = "Email__AzureCommunicationServices__ConnectionString"
          secret_name = "email-connection-string"
        }
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
  workload_profile_name        = "Consumption"

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

    cooldown_period_in_seconds = local.cooldown_seconds

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      # The api app's internal address, api.internal.<environment domain>,
      # over https (#111). Not the short name http://api: that is plain http,
      # which the api's ingress no longer accepts.
      env {
        name  = "API_URL"
        value = "https://${azurerm_container_app.api.ingress[0].fqdn}"
      }
      # https, so the session cookies are Secure (web/src/lib/session.ts).
      env {
        name  = "PUBLIC_BASE_URL"
        value = local.web_url
      }
      # Shown on the privacy notice (#51); empty is allowed.
      env {
        name  = "PRIVACY_CONTACT_EMAIL"
        value = var.privacy_contact_email
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

# Scheduled jobs (ADR-0019, #112). Nothing in the PoC runs all the time, so
# work on a timer is a Container Apps Job: it starts the API image with
# `job <name>`, which runs that job against the database and exits. Schedules
# are cron in UTC. Adding a job is a class in api/src/SocialTennis.Api/Jobs and
# a line here.
locals {
  jobs = {
    # Deletes used or expired sign-in links and ended sessions.
    "purge-tokens" = "0 3 * * *"
  }
}

resource "azurerm_container_app_job" "scheduled" {
  for_each = local.jobs

  name                         = "job-${each.key}"
  location                     = var.location
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.this.id
  workload_profile_name        = "Consumption"

  # A job is short; one that hangs is stopped, and one that fails is retried
  # once. A failure is in the job's execution history and in Log Analytics.
  replica_timeout_in_seconds = 300
  replica_retry_limit        = 1

  schedule_trigger_config {
    cron_expression = each.value
  }

  secret {
    name  = "database-connection-string"
    value = var.database_connection_string
  }

  template {
    container {
      name   = "job"
      image  = var.api_image
      cpu    = 0.25
      memory = "0.5Gi"
      # Appended to the image's entrypoint (`dotnet SocialTennis.Api.dll`).
      args = ["job", each.key]

      env {
        name        = "ConnectionStrings__Default"
        secret_name = "database-connection-string"
      }
    }
  }
}

output "web_url" {
  description = "Public address of the app."
  value       = local.web_url
}

output "default_web_url" {
  description = "The address Azure assigns to the web app."
  value       = local.default_web_url
}

output "web_app_id" {
  description = "Id of the web Container App."
  value       = azurerm_container_app.web.id
}

output "web_custom_domain_verification_id" {
  description = "The value Azure expects in a custom hostname's asuid TXT record."
  value       = azurerm_container_app.web.custom_domain_verification_id
}

output "environment_id" {
  description = "Id of the Container Apps environment."
  value       = azurerm_container_app_environment.this.id
}

output "environment_static_ip" {
  description = "The environment's public address."
  value       = azurerm_container_app_environment.this.static_ip_address
}

output "api_app_name" {
  description = "Name of the api Container App."
  value       = azurerm_container_app.api.name
}
