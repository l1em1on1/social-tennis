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

variable "always_on" {
  description = "Keep one replica of web and api running. Removes cold starts and leaves the free grant (ADR-0019)."
  type        = bool
  default     = false
}

variable "alert_email" {
  description = "Where the budget alert is sent."
  type        = string
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
