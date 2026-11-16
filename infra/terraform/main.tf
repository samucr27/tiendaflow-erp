terraform {
  required_version = ">= 1.6.0"

  # Remote state in HCP Terraform so the whole team shares one state.
  # The workspace runs in "Local" execution mode: Terraform runs on the
  # operator machine with an active `az login` and HCP only stores the state.
  cloud {
    organization = "Tienda-Flow"

    workspaces {
      name = "tiendaflow-infra"
    }
  }

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }

    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

provider "azurerm" {
  features {}
}

locals {
  suffix = random_string.suffix.result

  resource_group_name = "rg-${var.name_prefix}-${local.suffix}"
  app_service_name    = "app-${var.name_prefix}-${local.suffix}"
  plan_name           = "plan-${var.name_prefix}-${local.suffix}"
  sql_server_name     = "sql-${var.name_prefix}-${local.suffix}"

  tags = {
    project     = "TiendaFlow"
    environment = "production"
    managed_by  = "terraform"
  }

  connection_string = join(";", [
    "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433",
    "Initial Catalog=${azurerm_mssql_database.main.name}",
    "Persist Security Info=False",
    "User ID=${var.sql_admin_login}",
    "Password=${var.sql_admin_password}",
    "MultipleActiveResultSets=False",
    "Encrypt=True",
    "TrustServerCertificate=False",
    "Connection Timeout=30"
  ])
}

resource "random_string" "suffix" {
  length  = 6
  lower   = true
  upper   = false
  numeric = true
  special = false
}

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

resource "azurerm_service_plan" "main" {
  name                = local.plan_name
  resource_group_name = azurerm_resource_group.main.name
  location            = coalesce(var.app_location, var.location) # CAMBIO
  os_type             = "Linux"
  sku_name            = var.app_service_sku
  tags                = local.tags
}

resource "azurerm_linux_web_app" "main" {
  name                                           = local.app_service_name
  resource_group_name                            = azurerm_resource_group.main.name
  location                                       = coalesce(var.app_location, var.location) # CAMBIO
  service_plan_id                                = azurerm_service_plan.main.id
  https_only                                     = true
  webdeploy_publish_basic_authentication_enabled = true

  site_config {
    always_on                         = true
    ftps_state                        = "Disabled"
    http2_enabled                     = true
    websockets_enabled                = true
    health_check_path                 = "/healthz"
    health_check_eviction_time_in_min = 10

    application_stack {
      dotnet_version = "9.0"
    }
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"               = "Production"
    "ASPNETCORE_FORWARDEDHEADERS_ENABLED"  = "true"
    "ConnectionStrings__DefaultConnection" = local.connection_string
    "Database__MigrateOnStartup"           = "false"
    "DataProtection__KeysPath"             = "/home/.tiendaflow/keys"
    "Seed__Enabled"                        = var.seed_enabled ? "true" : "false"
    "Seed__AdminPassword"                  = var.seed_enabled ? var.seed_admin_password : ""
    "Seed__DemoPassword"                   = var.seed_enabled ? var.seed_demo_password : ""
  }

  tags = local.tags
}

resource "azurerm_mssql_server" "main" {
  name                          = local.sql_server_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = azurerm_resource_group.main.location
  version                       = "12.0"
  administrator_login           = var.sql_admin_login
  administrator_login_password  = var.sql_admin_password
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true

  tags = local.tags
}

resource "azurerm_mssql_database" "main" {
  name        = var.sql_database_name
  server_id   = azurerm_mssql_server.main.id
  collation   = "SQL_Latin1_General_CP1_CI_AS"
  max_size_gb = 2
  sku_name    = var.sql_database_sku
  tags        = local.tags
}

# The App Service reaches Azure SQL through its public endpoint, so the
# "allow Azure services" rule is required. It does not open the database to
# the internet by itself: Azure still rejects connections from outside the
# platform unless another firewall rule matches.
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Optional rule for the operator machine. It is what makes the EF migration
# bundle work from a laptop without a jump box or a VPN.
resource "azurerm_mssql_firewall_rule" "client" {
  count = var.client_ip_address == "" ? 0 : 1

  name             = "AllowClientIp"
  server_id        = azurerm_mssql_server.main.id
  start_ip_address = var.client_ip_address
  end_ip_address   = var.client_ip_address
}