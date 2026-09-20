output "gcp_project_id" {
  description = "Google Cloud project ID managed by this Terraform root."
  value       = var.gcp_project_id
}

output "gcp_region" {
  description = "Primary Google Cloud region for Atlas Supply production resources."
  value       = var.gcp_region
}

output "api_runtime_service_account_email" {
  description = "Email address of the API runtime service account."
  value       = google_service_account.api_runtime.email
}

output "mcp_runtime_service_account_email" {
  description = "Email address of the MCP runtime service account."
  value       = google_service_account.mcp_runtime.email
}

output "github_deploy_service_account_email" {
  description = "Email address of the GitHub deployment service account."
  value       = google_service_account.github_deploy.email
}

output "github_workload_identity_provider" {
  description = "Full Workload Identity Provider resource name for google-github-actions/auth."
  value       = google_iam_workload_identity_pool_provider.github.name
}

output "cloudflare_pages_project_name" {
  description = "Name of the Cloudflare Pages project."
  value       = cloudflare_pages_project.web.name
}

output "cloudflare_pages_project_id" {
  description = "Cloudflare Pages project identifier."
  value       = cloudflare_pages_project.web.id
}

output "cloudflare_pages_url" {
  description = "Canonical Cloudflare Pages URL for the project."
  value       = format("https://%s", cloudflare_pages_project.web.subdomain)
}

output "cloud_run_api_service_name" {
  description = "Name of the API Cloud Run service."
  value       = google_cloud_run_v2_service.api.name
}

output "cloud_run_api_url" {
  description = "URI of the API Cloud Run service."
  value       = google_cloud_run_v2_service.api.uri
}

output "cloud_run_mcp_service_name" {
  description = "Name of the MCP Cloud Run service."
  value       = google_cloud_run_v2_service.mcp.name
}

output "cloud_run_mcp_url" {
  description = "URI of the MCP Cloud Run service."
  value       = google_cloud_run_v2_service.mcp.uri
}
