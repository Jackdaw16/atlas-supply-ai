terraform {
  required_version = ">= 1.9, < 2.0"

  cloud {
    organization = "Demetrios"

    workspaces {
      name = "atlas-supply-ai"
    }
  }

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 6.0"
    }

    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.0"
    }
  }
}
