resource "google_cloud_run_v2_service" "api" {
  name     = "atlas-supply-api"
  location = var.gcp_region
  project  = var.gcp_project_id
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.api_runtime.email

    scaling {
      min_instance_count = 0
      max_instance_count = 2
    }

    containers {
      image = var.cloud_run_api_image

      ports {
        container_port = 8080
      }

      resources {
        cpu_idle = true
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].containers[0].image,
      template[0].containers[0].env,
    ]
  }

  depends_on = [
    google_project_service.required["run.googleapis.com"],
    google_service_account_iam_member.terraform_hcp_runtime_service_account_user["api"],
  ]
}

resource "google_cloud_run_v2_service" "mcp" {
  name     = "atlas-supply-mcp"
  location = var.gcp_region
  project  = var.gcp_project_id
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.mcp_runtime.email

    scaling {
      min_instance_count = 0
      max_instance_count = 2
    }

    containers {
      image = var.cloud_run_mcp_image

      ports {
        container_port = 8080
      }

      resources {
        cpu_idle = true
      }
    }
  }

  lifecycle {
    ignore_changes = [
      template[0].containers[0].image,
      template[0].containers[0].env,
    ]
  }

  depends_on = [
    google_project_service.required["run.googleapis.com"],
    google_service_account_iam_member.terraform_hcp_runtime_service_account_user["mcp"],
  ]
}
