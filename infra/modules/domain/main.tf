# The app's own address (ADR-0021): the web app answers on the club's domain,
# over https, with a certificate Azure issues and renews for free.
#
# Like email, it takes two applies with a manual step between:
#
#   1. Creates the DNS records, adds the hostname to the web app, and has
#      Azure issue the certificate.
#   2. scripts/setup-domain.sh binds the certificate to the hostname. The
#      provider can't: its custom domain resource can only name a certificate
#      at creation, and Azure won't issue one until the hostname is on the app.
#   3. app_domain_ready = true (in the root) makes the domain the app's public
#      address.

terraform {
  required_providers {
    cloudflare = {
      source = "cloudflare/cloudflare"
    }
  }
}

variable "domain" {
  description = "Hostname the app is served at. The apex or a subdomain of zone."
  type        = string
}

variable "zone" {
  description = "Cloudflare zone the hostname's records are created in."
  type        = string
}

variable "web_app_id" {
  description = "Id of the web Container App."
  type        = string
}

variable "web_custom_domain_verification_id" {
  description = "The value Azure expects in the asuid TXT record."
  type        = string
}

variable "environment_id" {
  description = "Id of the Container Apps environment, which holds the certificate."
  type        = string
}

variable "environment_static_ip" {
  description = "The environment's public address."
  type        = string
}

data "cloudflare_zone" "this" {
  filter = {
    name = var.zone
  }
}

# An A record, not a CNAME: it is the only kind an apex can hold, and it works
# for a subdomain too. DNS only, never proxied: Azure ends the TLS connection,
# and checks the certificate request over plain http at this address.
resource "cloudflare_dns_record" "app" {
  zone_id = data.cloudflare_zone.this.id
  name    = var.domain
  type    = "A"
  content = var.environment_static_ip
  ttl     = 300
  proxied = false
  comment = "The app (Azure Container Apps)"
}

# Proves to Azure that the hostname is ours to put on the app.
resource "cloudflare_dns_record" "app_verification" {
  zone_id = data.cloudflare_zone.this.id
  name    = "asuid.${var.domain}"
  type    = "TXT"
  content = "\"${var.web_custom_domain_verification_id}\""
  ttl     = 300
  comment = "Azure Container Apps: hostname ownership"
}

# Azure looks the TXT record up the moment the hostname is added, and fails the
# apply if Cloudflare's nameservers aren't serving it yet: the first deploy
# asked 2.5 seconds after the record was created. A fixed wait, because nothing
# in the apply can ask Azure's resolver what it sees.
resource "terraform_data" "app_verification_published" {
  triggers_replace = cloudflare_dns_record.app_verification.id

  provisioner "local-exec" {
    command = "sleep 60"
  }
}

resource "azurerm_container_app_custom_domain" "web" {
  name             = var.domain
  container_app_id = var.web_app_id

  # The certificate is bound outside Terraform (step 2 above). Without this
  # the next apply would see the binding as drift and recreate the hostname.
  lifecycle {
    ignore_changes = [certificate_binding_type, container_app_environment_certificate_id]
  }

  depends_on = [terraform_data.app_verification_published]
}

# Issued and renewed by Azure. HTTP validation: Azure fetches a file from the
# hostname, which needs the A record and the hostname on the app.
resource "azurerm_container_app_environment_managed_certificate" "web" {
  name                         = replace(var.domain, ".", "-")
  container_app_environment_id = var.environment_id
  subject_name                 = var.domain
  domain_control_validation    = "HTTP"

  depends_on = [cloudflare_dns_record.app, azurerm_container_app_custom_domain.web]
}

output "certificate_name" {
  description = "Name of the managed certificate, for the bind command."
  value       = azurerm_container_app_environment_managed_certificate.web.name
}
