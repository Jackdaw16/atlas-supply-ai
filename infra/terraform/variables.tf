variable "gcp_project_id" {
  type        = string
  description = "Existing Google Cloud project ID that Terraform manages."
  nullable    = false

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{4,28}[a-z0-9]$", var.gcp_project_id))
    error_message = "gcp_project_id must be a 6-30 character lowercase Google Cloud project ID."
  }
}

variable "gcp_region" {
  type        = string
  description = "Primary Google Cloud region for Atlas Supply production resources."
  default     = "europe-southwest1"
  nullable    = false
}

variable "cloud_run_api_image" {
  type        = string
  description = "Container image URI deployed to the API Cloud Run service."
  nullable    = false

  validation {
    condition     = trimspace(var.cloud_run_api_image) != ""
    error_message = "cloud_run_api_image must not be blank."
  }
}

variable "cloud_run_mcp_image" {
  type        = string
  description = "Container image URI deployed to the MCP Cloud Run service."
  nullable    = false

  validation {
    condition     = trimspace(var.cloud_run_mcp_image) != ""
    error_message = "cloud_run_mcp_image must not be blank."
  }
}

variable "github_repository" {
  type        = string
  description = "GitHub owner/repository authorized to federate with Google Cloud."
  default     = "Jackdaw16/atlas-supply-ai"
  nullable    = false

  validation {
    condition     = can(regex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?/[A-Za-z0-9][A-Za-z0-9_.-]*$", var.github_repository))
    error_message = "github_repository must be a GitHub owner/repository value."
  }
}

variable "cloudflare_account_id" {
  type        = string
  description = "Cloudflare account ID that owns the Pages project."
  nullable    = false

  validation {
    condition     = can(regex("^[0-9a-fA-F]{32}$", var.cloudflare_account_id))
    error_message = "cloudflare_account_id must be a 32-character hexadecimal Cloudflare account ID."
  }
}

variable "cloudflare_pages_project_name" {
  type        = string
  description = "Name of the Cloudflare Pages project to create."
  default     = "atlas-supply-web"
  nullable    = false

  validation {
    condition     = can(regex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$", var.cloudflare_pages_project_name))
    error_message = "cloudflare_pages_project_name must be 1-63 lowercase letters, numbers, or hyphens, starting and ending with a letter or number."
  }
}

variable "cloudflare_pages_production_branch" {
  type        = string
  description = "Git branch Cloudflare Pages treats as the production branch."
  default     = "main"
  nullable    = false

  validation {
    condition     = can(regex("^[A-Za-z0-9][A-Za-z0-9._/-]*$", var.cloudflare_pages_production_branch))
    error_message = "cloudflare_pages_production_branch must begin with a letter or number and contain only letters, numbers, periods, underscores, slashes, or hyphens."
  }
}
