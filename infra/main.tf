# PoC hosting (ADR-0019): Azure Container Apps + Neon Postgres, scaled to zero.
#
#   Browser ──https──> web (public ingress)
#                        │ http://api, inside the Container Apps environment
#                        v
#                      api (internal ingress only)
#                        │ TLS, direct connection
#                        v
#                      Neon Postgres

locals {
  name = "social-tennis"
}

resource "azurerm_resource_group" "this" {
  name     = local.name
  location = var.region
}

module "database" {
  source = "./modules/database"

  name        = local.name
  neon_org_id = var.neon_org_id
  neon_region = var.neon_region
}

module "compute" {
  source = "./modules/compute"

  name                = local.name
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name

  web_image = "${var.image_repository_prefix}-web:${var.image_tag}"
  api_image = "${var.image_repository_prefix}-api:${var.image_tag}"
  always_on = var.always_on

  database_connection_string = module.database.connection_string
  database_name              = module.database.database_name
}

# Leaving the free grant should arrive as an email, not as a surprise invoice.
resource "azurerm_consumption_budget_resource_group" "this" {
  name              = "${local.name}-budget"
  resource_group_id = azurerm_resource_group.this.id

  amount     = var.monthly_budget
  time_grain = "Monthly"

  time_period {
    start_date = var.budget_start_date
  }

  notification {
    operator       = "GreaterThanOrEqualTo"
    threshold      = 100
    threshold_type = "Actual"
    contact_emails = [var.alert_email]
  }

  notification {
    operator       = "GreaterThanOrEqualTo"
    threshold      = 100
    threshold_type = "Forecasted"
    contact_emails = [var.alert_email]
  }
}
