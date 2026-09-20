resource "google_service_account_iam_member" "github_deploy_workload_identity_user" {
  service_account_id = google_service_account.github_deploy.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github.name}/attribute.repository/${var.github_repository}"
}

resource "google_project_iam_member" "github_deploy" {
  for_each = toset([
    "roles/run.admin",
    "roles/serviceusage.serviceUsageConsumer",
  ])

  project = var.gcp_project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.github_deploy.email}"
}

resource "google_service_account_iam_member" "runtime_service_account_user" {
  for_each = {
    api = google_service_account.api_runtime.name
    mcp = google_service_account.mcp_runtime.name
  }

  service_account_id = each.value
  role               = "roles/iam.serviceAccountUser"
  member             = "serviceAccount:${google_service_account.github_deploy.email}"
}
