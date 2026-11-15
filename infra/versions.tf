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
    # DNS records for the sending domain (ADR-0020) and the app's address
    # (ADR-0021).
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.19"
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
  features {
    # Storage accounts are managed through Azure Resource Manager only. The
    # backup account accepts Entra ID sign-in only (#137), and the deploy
    # identity has no data-plane role; nothing here uses queue properties or
    # static websites, the only settings that need the data plane.
    storage {
      data_plane_available = false
    }
  }
}

# Reads NEON_API_KEY from the environment.
provider "neon" {}

# Reads CLOUDFLARE_API_TOKEN from the environment. Only contacted when
# email_domain or app_domain is set.
provider "cloudflare" {}
