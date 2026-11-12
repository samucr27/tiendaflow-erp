output "app_url" {
  description = "Public HTTPS URL of the deployed application."
  value       = "https://${azurerm_linux_web_app.main.default_hostname}"
}

output "app_service_name" {
  description = "Name of the Linux web app, used by the deployment commands."
  value       = azurerm_linux_web_app.main.name
}

output "resource_group_name" {
  description = "Resource group that contains the deployment."
  value       = azurerm_resource_group.main.name
}

output "sql_server_fqdn" {
  description = "Fully qualified domain name of the Azure SQL server."
  value       = azurerm_mssql_server.main.fully_qualified_domain_name
}

output "sql_database_name" {
  description = "Name of the application database."
  value       = azurerm_mssql_database.main.name
}

output "sql_server_name" {
  description = "Name of the Azure SQL server."
  value       = azurerm_mssql_server.main.name
}
