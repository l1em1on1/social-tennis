output "url" {
  description = "Public address of the app."
  value       = module.compute.web_url
}

output "resource_group" {
  description = "Resource group holding the Azure side, for az commands."
  value       = azurerm_resource_group.this.name
}

output "api_app_name" {
  description = "Name of the api Container App, for reading sign-in links from its logs."
  value       = module.compute.api_app_name
}
