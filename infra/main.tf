# PoC hosting (ADR-0019): Azure Container Apps + Neon Postgres, scaled to zero.
#
#   Browser ──https──> web (public ingress)
#                        │ http://api, inside the Container Apps environment
#                        v
#                      api (internal ingress only)
#                        │ TLS, direct connection
#                        v
#                      Neon Postgres
#
# The app can also be served at the club's own domain, with DNS in Cloudflare
# and a certificate managed by Azure (ADR-0021).
#
# The api also sends the sign-in emails, through Azure Communication Services,
# once email_domain is set and verified (ADR-0020).

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

# Absent until a domain is named, so the app can be deployed without one.
module "email" {
  source = "./modules/email"
  count  = var.email_domain == "" ? 0 : 1

  name                = local.name
  resource_group_name = azurerm_resource_group.this.name
  resource_group_id   = azurerm_resource_group.this.id
  domain              = var.email_domain
  domain_verified     = var.email_domain_verified
}

locals {
  email_sending = one(module.email[*].sending_enabled) == true
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

  public_url = var.app_domain != "" && var.app_domain_ready ? "https://${var.app_domain}" : null

  email_enabled           = local.email_sending
  email_connection_string = local.email_sending ? module.email[0].connection_string : null
  email_from_address      = local.email_sending ? module.email[0].from_address : null
}

# Absent until a hostname is named. The app keeps answering on Azure's address
# as well; app_domain_ready only decides which one it calls its own.
module "domain" {
  source = "./modules/domain"
  count  = var.app_domain == "" ? 0 : 1

  domain = var.app_domain
  zone   = coalesce(var.app_domain_zone, var.email_domain, var.app_domain)

  web_app_id                        = module.compute.web_app_id
  web_custom_domain_verification_id = module.compute.web_custom_domain_verification_id
  environment_id                    = module.compute.environment_id
  environment_static_ip             = module.compute.environment_static_ip
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
