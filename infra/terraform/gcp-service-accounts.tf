resource "google_service_account" "api_runtime" {
  project      = var.gcp_project_id
  account_id   = "atlas-supply-api-runtime"
  display_name = "Atlas Supply API Runtime"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "mcp_runtime" {
  project      = var.gcp_project_id
  account_id   = "atlas-supply-mcp-runtime"
  display_name = "Atlas Supply MCP Runtime"

  depends_on = [google_project_service.required]
}

resource "google_service_account" "github_deploy" {
  project      = var.gcp_project_id
  account_id   = "atlas-supply-github-deploy"
  display_name = "Atlas Supply GitHub Deploy"

  depends_on = [google_project_service.required]
}
