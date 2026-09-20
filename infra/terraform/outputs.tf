output "gcp_project_id" {
  description = "Google Cloud project ID managed by this Terraform root."
  value       = var.gcp_project_id
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
