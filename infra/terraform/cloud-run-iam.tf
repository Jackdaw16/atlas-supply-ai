resource "google_cloud_run_v2_service_iam_member" "api_public_invoker" {
  project  = var.gcp_project_id
  location = google_cloud_run_v2_service.api.location
  name     = google_cloud_run_v2_service.api.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}

resource "google_cloud_run_v2_service_iam_member" "mcp_api_invoker" {
  project  = var.gcp_project_id
  location = google_cloud_run_v2_service.mcp.location
  name     = google_cloud_run_v2_service.mcp.name
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.api_runtime.email}"
}
