# Email (ADR-0020): Azure Communication Services sends the sign-in emails from
# the club's own domain, and the DNS records that prove the domain is ours are
# created in its Cloudflare zone.
#
# It takes two applies, because Azure checks the DNS records between them:
#
#   1. domain_verified = false  creates the mail resources and the DNS records.
#   2. scripts/setup-domain.sh  asks Azure to check the records and waits.
#   3. domain_verified = true   links the domain to the sending resource. Only
#                               then is there anything for the API to send with.
#
# The provider has no resource for step 2, and Azure refuses to link a domain
# it hasn't verified, so one apply can't do all three.

terraform {
  required_providers {
    cloudflare = {
      source = "cloudflare/cloudflare"
    }
  }
}

variable "name" {
  description = "Prefix for resource names."
  type        = string
}

variable "resource_group_name" {
  description = "Resource group to create everything in."
  type        = string
}

variable "resource_group_id" {
  description = "Id of that resource group. Seeds the suffix that makes the globally unique names unique."
  type        = string
}

variable "domain" {
  description = "Domain the email is sent from. Its DNS zone must be in the Cloudflare account the API token belongs to."
  type        = string
}

variable "domain_verified" {
  description = "Azure has verified the DNS records (scripts/setup-domain.sh). Links the domain so email can be sent."
  type        = bool
}

locals {
  # Communication Services names are unique across Azure, not per subscription.
  suffix = substr(sha1(var.resource_group_id), 0, 6)

  # Azure reports each record's name relative to the domain; Cloudflare wants
  # the full name.
  records = azurerm_email_communication_service_domain.this.verification_records[0]
  dkim = {
    for key, record in { dkim = local.records.dkim[0], dkim2 = local.records.dkim2[0] } :
    key => {
      name  = endswith(record.name, var.domain) ? record.name : "${record.name}.${var.domain}"
      value = record.value
    }
  }
}

resource "azurerm_email_communication_service" "this" {
  name                = "${var.name}-email-${local.suffix}"
  resource_group_name = var.resource_group_name
  data_location       = "UK"
}

resource "azurerm_email_communication_service_domain" "this" {
  name              = var.domain
  email_service_id  = azurerm_email_communication_service.this.id
  domain_management = "CustomerManaged"
}

# The resource the API holds a connection string for.
resource "azurerm_communication_service" "this" {
  name                = "${var.name}-communication-${local.suffix}"
  resource_group_name = var.resource_group_name
  data_location       = "UK"
}

resource "azurerm_communication_service_email_domain_association" "this" {
  count = var.domain_verified ? 1 : 0

  communication_service_id = azurerm_communication_service.this.id
  email_service_domain_id  = azurerm_email_communication_service_domain.this.id
}

data "cloudflare_zone" "this" {
  filter = {
    name = var.domain
  }
}

# Proves to Azure that the domain is ours.
resource "cloudflare_dns_record" "domain_verification" {
  zone_id = data.cloudflare_zone.this.id
  name    = var.domain
  type    = "TXT"
  content = "\"${local.records.domain[0].value}\""
  ttl     = 3600
  comment = "Azure Communication Services: domain ownership"
}

# SPF: Azure's servers may send as this domain. A domain may have only one SPF
# record: if the zone ever sends mail another way too, merge the two here.
resource "cloudflare_dns_record" "spf" {
  zone_id = data.cloudflare_zone.this.id
  name    = var.domain
  type    = "TXT"
  content = "\"${local.records.spf[0].value}\""
  ttl     = 3600
  comment = "Azure Communication Services: SPF"
}

# DKIM: Azure signs each email, and these point at the keys that check it.
# Not proxied: a proxied CNAME answers with Cloudflare's addresses.
resource "cloudflare_dns_record" "dkim" {
  for_each = local.dkim

  zone_id = data.cloudflare_zone.this.id
  name    = each.value.name
  type    = "CNAME"
  content = each.value.value
  ttl     = 3600
  proxied = false
  comment = "Azure Communication Services: DKIM"
}

# DMARC: tells receivers a policy exists, which Gmail and Outlook expect of a
# sender. p=none only reports; tighten it to quarantine once mail is flowing.
resource "cloudflare_dns_record" "dmarc" {
  zone_id = data.cloudflare_zone.this.id
  name    = "_dmarc.${var.domain}"
  type    = "TXT"
  content = "\"v=DMARC1; p=none\""
  ttl     = 3600
  comment = "DMARC policy for mail from this domain"
}

output "sending_enabled" {
  description = "The domain is linked, so the connection string can send."
  value       = var.domain_verified
}

output "connection_string" {
  description = "Connection string of the Communication Services resource."
  value       = azurerm_communication_service.this.primary_connection_string
  sensitive   = true

  # Handing it out before the link exists would start an API that can't send.
  depends_on = [azurerm_communication_service_email_domain_association.this]
}

output "from_address" {
  description = "Sender address. Azure creates DoNotReply on every custom domain."
  value       = "DoNotReply@${var.domain}"
}

output "email_service_name" {
  description = "Name of the Email Communication Service, for az commands."
  value       = azurerm_email_communication_service.this.name
}
