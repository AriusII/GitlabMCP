using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Terraform state protection, cluster-agent tokens, dependency scanning, error tracking and
    // attestations (InfraTools.cs) — all DevOps per CLAUDE.md's persona table ("Terraform, runners,
    // and the surrounding infrastructure").
    private static IReadOnlyDictionary<string, ToolGrant> InfraRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_dependencies"] = new(Grant.DevOps, true),
            ["gitlab_list_cluster_agent_tokens"] = new(Grant.DevOps, true),
            ["gitlab_list_terraform_state_protection_rules"] = new(Grant.DevOps, true),
            ["gitlab_list_error_tracking_client_keys"] = new(Grant.DevOps, true),
            ["gitlab_download_attestation"] = new(Grant.DevOps, true),
            ["gitlab_create_terraform_state_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_create_cluster_agent_token"] = new(Grant.DevOps, false),
            ["gitlab_create_error_tracking_client_key"] = new(Grant.DevOps, false),

            // Backlog part 1/2: Terraform states (document/lock/version), cluster agents, cluster
            // agent tokens/URL configurations, dependency exports.
            ["gitlab_get_terraform_state"] = new(Grant.DevOps, true),
            ["gitlab_upload_terraform_state"] = new(Grant.DevOps, false),
            ["gitlab_delete_terraform_state"] = new(Grant.DevOps, false),
            ["gitlab_delete_terraform_state_version"] = new(Grant.DevOps, false),
            ["gitlab_lock_terraform_state"] = new(Grant.DevOps, false),
            ["gitlab_unlock_terraform_state"] = new(Grant.DevOps, false),
            ["gitlab_update_terraform_state_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_delete_terraform_state_protection_rule"] = new(Grant.DevOps, false),
            ["gitlab_list_cluster_agents"] = new(Grant.DevOps, true),
            ["gitlab_get_cluster_agent"] = new(Grant.DevOps, true),
            ["gitlab_register_cluster_agent"] = new(Grant.DevOps, false),
            ["gitlab_delete_cluster_agent"] = new(Grant.DevOps, false),
            ["gitlab_revoke_cluster_agent_token"] = new(Grant.DevOps, false),
            ["gitlab_get_cluster_agent_url_configuration"] = new(Grant.DevOps, true),
            ["gitlab_create_cluster_agent_url_configuration"] = new(Grant.DevOps, false),
            ["gitlab_delete_cluster_agent_url_configuration"] = new(Grant.DevOps, false),
            ["gitlab_export_dependencies"] = new(Grant.DevOps, false),
            ["gitlab_get_dependency_export_status"] = new(Grant.DevOps, true),

            // Backlog part 2/2: dependency export download, occurrence vulnerabilities, dependency
            // attestations, real-time SAST file scan, SBOM scans, error tracking settings, alert
            // metric images.
            ["gitlab_download_dependency_export"] = new(Grant.DevOps, true),
            ["gitlab_list_dependency_occurrence_vulnerabilities"] = new(Grant.DevOps, true),
            ["gitlab_list_dependency_attestations"] = new(Grant.DevOps, true),
            ["gitlab_scan_file_for_vulnerabilities"] = new(Grant.DevOps, true),
            ["gitlab_upload_sbom_scan"] = new(Grant.DevOps, false),
            ["gitlab_get_sbom_scan"] = new(Grant.DevOps, true),
            ["gitlab_reuse_sbom_scan"] = new(Grant.DevOps, false),
            ["gitlab_delete_error_tracking_client_key"] = new(Grant.DevOps, false),
            ["gitlab_get_error_tracking_settings"] = new(Grant.DevOps, true),
            ["gitlab_create_error_tracking_settings"] = new(Grant.DevOps, false),
            ["gitlab_update_error_tracking_settings"] = new(Grant.DevOps, false),
            ["gitlab_list_alert_metric_images"] = new(Grant.DevOps, true),
            ["gitlab_upload_alert_metric_image"] = new(Grant.DevOps, false),
            ["gitlab_update_alert_metric_image"] = new(Grant.DevOps, false),
            ["gitlab_delete_alert_metric_image"] = new(Grant.DevOps, false)
        };
    }
}