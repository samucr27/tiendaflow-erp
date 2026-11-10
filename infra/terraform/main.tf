terraform {
  required_version = ">= 1.6.0"

  # Remote state in HCP Terraform so the whole team shares one state
  # and plans never conflict on a local file.
  cloud {
    organization = "Tienda-Flow"

    workspaces {
      name = "tiendaflow-infra"
    }
  }

  required_providers {
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

# Placeholder resource: it demonstrates the remote state and the automated
# plan without needing cloud credentials. Replace it with the real
# infrastructure (database, app hosting) when the deployment target is chosen.
resource "random_pet" "environment" {
  length = 2
}

output "environment_name" {
  value = "tiendaflow-${random_pet.environment.id}"
}