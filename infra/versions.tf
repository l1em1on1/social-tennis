terraform {
  required_version = "~> 1.17"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.14"
    }
    # Community-maintained (not published by Neon, though Neon's docs use it).
    # Pinned exactly; the database module is the only place it is used, so
    # replacing it is a local change (ADR-0019).
    neon = {
      source  = "kislerdm/neon"
      version = "0.18.0"
    }
  }

  # resource_group_name and storage_account_name are passed by init.sh.
  backend "azurerm" {
    container_name   = "tfstate"
    key              = "social-tennis.tfstate"
    use_azuread_auth = true
  }
}

provider "azurerm" {
  features {}
}

# Reads NEON_API_KEY from the environment.
provider "neon" {}
