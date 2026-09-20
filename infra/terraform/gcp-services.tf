moved {
  from = google_project_service.service_usage
  to   = google_project_service.required["serviceusage.googleapis.com"]
}

locals {
  required_gcp_services = toset([
    "serviceusage.googleapis.com",
    "run.googleapis.com",
    "iam.googleapis.com",
    "iamcredentials.googleapis.com",
    "sts.googleapis.com",
  ])
}

resource "google_project_service" "required" {
  for_each = local.required_gcp_services

  project = var.gcp_project_id
  service = each.value

  disable_on_destroy = false
}
