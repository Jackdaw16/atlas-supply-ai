resource "google_project_service" "service_usage" {
  project = var.gcp_project_id
  service = "serviceusage.googleapis.com"

  disable_on_destroy = false
}
