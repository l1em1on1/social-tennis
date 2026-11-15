output "url" {
  description = "Public address of the app."
  value       = module.compute.web_url
}

output "default_url" {
  description = "The address Azure assigns, which keeps working beside a custom domain."
  value       = module.compute.default_web_url
}

output "resource_group" {
  description = "Resource group holding the Azure side, for az commands."
  value       = azurerm_resource_group.this.name
}

output "api_app_name" {
  description = "Name of the api Container App, for reading sign-in links from its logs."
  value       = module.compute.api_app_name
}

output "email_from_address" {
  description = "Address sign-in emails are sent from, or null while the API still logs them."
  value       = local.email_sending ? module.email[0].from_address : null
}

output "backup_storage_account" {
  description = "Storage account holding the database dumps, under backups/dumps/."
  value       = module.backup.storage_account_name
}

output "backup_container_id" {
  description = "Resource id of the dumps' container: the scope for the backup job's role, and for downloading a dump."
  value       = module.backup.container_id
}

output "backup_principal_id" {
  description = "Object id of the backup job's identity, which the owner grants Storage Blob Data Contributor once (README, \"Backups\")."
  value       = module.backup.principal_id
}
