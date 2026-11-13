variable "location" {
  description = "Azure region for every resource."
  type        = string
  default     = "canadacentral"

  validation {
    condition = contains(
      ["canadacentral", "chilecentral", "spaincentral", "belgiumcentral", "francecentral"],
      var.location
    )

    error_message = "La suscripción Azure for Students de la universidad solo permite estas regiones: canadacentral, chilecentral, spaincentral, belgiumcentral y francecentral. Cambia `location` a una de ellas."
  }
}

variable "name_prefix" {
  description = "Prefix used for the resource group, App Service plan, web app and SQL server names."
  type        = string
  default     = "tiendaflow"

  validation {
    condition     = can(regex("^[a-z0-9-]{3,24}$", var.name_prefix))
    error_message = "name_prefix must contain only lowercase letters, digits and hyphens (3-24 characters)."
  }
}

variable "app_service_sku" {
  description = "Linux App Service plan size. B1 is the recommended student size."
  type        = string
  default     = "B1"

  validation {
    condition     = contains(["B1", "B2", "B3", "S1", "S2", "S3"], var.app_service_sku)
    error_message = "app_service_sku must be one of B1, B2, B3, S1, S2 or S3."
  }
}

variable "sql_database_sku" {
  description = "Azure SQL database service tier."
  type        = string
  default     = "Basic"

  validation {
    condition     = contains(["Basic", "S0", "S1", "S2"], var.sql_database_sku)
    error_message = "sql_database_sku must be one of Basic, S0, S1 or S2."
  }
}

variable "sql_database_name" {
  description = "Name of the application database."
  type        = string
  default     = "TiendaFlow"
}

variable "sql_admin_login" {
  description = "Administrator login of the Azure SQL server."
  type        = string
  default     = "tiendaflowadmin"

  validation {
    condition     = can(regex("^[A-Za-z][A-Za-z0-9_]{2,63}$", var.sql_admin_login))
    error_message = "sql_admin_login must start with a letter and contain only letters, digits and underscores."
  }
}

variable "sql_admin_password" {
  description = "Administrator password of the Azure SQL server. Set it as a sensitive HCP workspace variable or with TF_VAR_sql_admin_password."
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.sql_admin_password) >= 8
    error_message = "sql_admin_password must have at least 8 characters."
  }
}

variable "seed_enabled" {
  description = "When true, the app seeds the demo users on startup. Keep it false in production unless you need the demo accounts."
  type        = bool
  default     = false
}

variable "seed_admin_password" {
  description = "Password for the seeded administrator user. Required only when seed_enabled is true."
  type        = string
  sensitive   = true
  default     = ""
}

variable "seed_demo_password" {
  description = "Password for the rest of the seeded demo users. Required only when seed_enabled is true."
  type        = string
  sensitive   = true
  default     = ""
}

variable "client_ip_address" {
  description = "Optional public IPv4 address of the operator machine, added to the Azure SQL firewall so the EF migration bundle can run from that machine. Leave empty to skip the rule."
  type        = string
  default     = ""

  validation {
    condition     = var.client_ip_address == "" || can(cidrhost("${var.client_ip_address}/32", 0))
    error_message = "client_ip_address must be empty or a valid IPv4 address, for example 190.0.0.10."
  }
}

variable "app_location" {
  description = "Region for the App Service plan and web app. Empty means the same as location."
  type        = string
  default     = ""
}