variable "region" {
  description = "Azure region for every resource."
  type        = string
  default     = "uksouth"
}

variable "neon_region" {
  description = "Neon region id. The default is AWS London, the closest Neon region to UK South; check https://neon.com/docs/introduction/regions."
  type        = string
  default     = "aws-eu-west-2"
}

variable "neon_org_id" {
  description = "Neon organisation the project is created in. Neon's docs say to always set it, so the project can't land in the wrong organisation."
  type        = string
}

variable "image_repository_prefix" {
  description = "Registry path the two images share: <prefix>-web and <prefix>-api."
  type        = string
  default     = "ghcr.io/l1em1on1/social-tennis"
}

variable "image_tag" {
  description = "Image tag to run, normally a commit SHA published by CI."
  type        = string
}

# The deploy workflow resolves the tag to these digests in its plan job, so
# the apply job runs exactly the images that were planned, even if a tag were
# pushed again in between (#111). Left unset (a plan run by hand), the tag
# alone picks the images.
variable "web_image_digest" {
  description = "Digest of the web image to run (sha256:...). Overrides image_tag for web when set."
  type        = string
  default     = null

  validation {
    condition     = var.web_image_digest == null || can(regex("^sha256:[0-9a-f]{64}$", var.web_image_digest))
    error_message = "web_image_digest must look like sha256:<64 hex characters>."
  }
}

variable "api_image_digest" {
  description = "Digest of the api image to run (sha256:...). Overrides image_tag for api when set."
  type        = string
  default     = null

  validation {
    condition     = var.api_image_digest == null || can(regex("^sha256:[0-9a-f]{64}$", var.api_image_digest))
    error_message = "api_image_digest must look like sha256:<64 hex characters>."
  }
}

variable "always_on" {
  description = "Keep one replica of web and api running. Removes cold starts and leaves the free grant (ADR-0019)."
  type        = bool
  default     = false
}

variable "long_cooldown" {
  description = "Keep web and api running for 30 minutes after the last request, not 5. Fewer cold starts; spends the free grant faster (ADR-0019)."
  type        = bool
  default     = false
}

variable "alert_email" {
  description = "Where the budget alert is sent."
  type        = string
}

variable "admin_emails" {
  description = "Email addresses that are Admins of the app, separated by commas. Whoever signs in with one can approve club members. Empty: nobody is an Admin."
  type        = string
  default     = ""
  sensitive   = true
}

variable "monthly_budget" {
  description = "Budget for the resource group, in the subscription's billing currency. The alert fires at 100% of it."
  type        = number
  default     = 5
}

variable "budget_start_date" {
  description = "First day of the month the budget starts counting from (RFC 3339). Azure rejects a date that isn't the first of a month."
  type        = string
  default     = "2026-11-01T00:00:00Z"
}

variable "email_domain" {
  description = "Domain sign-in emails are sent from (ADR-0020). Its DNS must be in Cloudflare. Empty: no email is set up, and sign-in links are read from the API's logs."
  type        = string
  default     = ""
}

variable "email_domain_verified" {
  description = "Azure has verified email_domain's DNS records; scripts/setup-domain.sh sets this. Until it is true the API keeps logging sign-in links."
  type        = bool
  default     = false
}

variable "app_domain" {
  description = "Hostname the app is served at (ADR-0021), for example hitsandgiggles.co.uk. Its DNS must be in Cloudflare. Empty: only the address Azure assigns."
  type        = string
  default     = ""
}

variable "app_domain_zone" {
  description = "Cloudflare zone app_domain belongs to. Empty: email_domain if set, otherwise app_domain itself. Only needed for a subdomain when no email_domain is set."
  type        = string
  default     = ""
}

variable "app_domain_ready" {
  description = "The certificate is bound and https works at app_domain; scripts/setup-domain.sh sets this. Makes app_domain the address in redirects and emailed links."
  type        = bool
  default     = false
}
