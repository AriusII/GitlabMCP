using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Deploy domain (runners, environments, deployments, releases — CLAUDE.md's DevOps persona table,
    // plus bare-AdminOnly rows for instance-wide/admin-only surface and the two Delivery rows shared
    // with Developer per the catalog's own "profiles" list).
    //
    // Grant.AdminOnly is combined with a persona bit ONLY when a row is genuinely meant to be reachable
    // BOTH directly under that persona AND (redundantly) under FullPermission — which is actually never
    // needed, since Grant.IsVisibleIn checks each bit independently (an OR of independent conditions,
    // not an AND-gate) and FullPermission's own check is "grant != Grant.None", already satisfied by
    // the persona bit alone. A row combining a persona bit with AdminOnly makes that row reachable under
    // the bare persona directly, silently defeating "instance administrator access required" — see
    // DECISIONS.md's review-findings note (18+26). Every row below whose tool [Description] states
    // "Requires administrator access" is bare Grant.AdminOnly, full stop.
    private static IReadOnlyDictionary<string, ToolGrant> DeployRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_group_releases"] = new(Grant.DevOps | Grant.Maintainer, true),
            ["gitlab_list_admin_runners"] = new(Grant.AdminOnly, true),
            ["gitlab_list_instance_pages_domains"] = new(Grant.AdminOnly, true),
            ["gitlab_list_runner_controller_scopes"] = new(Grant.AdminOnly, true),
            ["gitlab_list_deployment_merge_requests"] = new(Grant.Delivery, true),
            ["gitlab_list_admin_deploy_keys"] = new(Grant.AdminOnly, true),
            ["gitlab_list_admin_deploy_tokens"] = new(Grant.AdminOnly, true),
            ["gitlab_list_secure_files"] = new(Grant.DevOps, true),
            ["gitlab_create_runner_controller_token"] = new(Grant.AdminOnly, false),
            ["gitlab_create_release"] = new(Grant.Delivery, false),
            ["gitlab_update_runner"] = new(Grant.DevOps, false),
            ["gitlab_create_environment"] = new(Grant.DevOps, false),
            ["gitlab_create_pages_domain"] = new(Grant.DevOps, false),
            ["gitlab_create_instance_deploy_key"] = new(Grant.AdminOnly, false),

            // IRunnersClient — every method here is project/group/instance scoped and reachable by a
            // DevOps-scoped token with the right project/group role; GitLab's own 403 is the ceiling for
            // the instance-wide edge case (CLAUDE.md "The GitLab token's own permissions are the real
            // ceiling"), so these carry the plain DevOps bit, never AdminOnly.
            ["gitlab_list_runners"] = new(Grant.DevOps, true),
            ["gitlab_get_runner"] = new(Grant.DevOps, true),
            ["gitlab_delete_runner"] = new(Grant.DevOps, false),
            ["gitlab_assign_runner_to_project"] = new(Grant.DevOps, false),
            ["gitlab_unassign_runner_from_project"] = new(Grant.DevOps, false),
            ["gitlab_list_runner_jobs"] = new(Grant.DevOps, true),
            ["gitlab_list_runner_managers"] = new(Grant.DevOps, true),
            ["gitlab_list_runner_projects"] = new(Grant.DevOps, true),
            ["gitlab_reset_runner_registration_token"] = new(Grant.DevOps, false),
            ["gitlab_reset_runner_authentication_token"] = new(Grant.DevOps, false),

            // IRunnerControllersClient — every method is keyed only by runnerControllerId, with no
            // ProjectId/GroupId anywhere on the interface: fleet-management agents are a purely
            // instance-level administration concept, so every row here is bare Grant.AdminOnly and every
            // tool's [Description] says so.
            ["gitlab_list_runner_controllers"] = new(Grant.AdminOnly, true),
            ["gitlab_get_runner_controller"] = new(Grant.AdminOnly, true),
            ["gitlab_register_runner_controller"] = new(Grant.AdminOnly, false),
            ["gitlab_update_runner_controller"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_runner_controller"] = new(Grant.AdminOnly, false),
            ["gitlab_assign_runner_controller_scope"] = new(Grant.AdminOnly, false),
            ["gitlab_remove_runner_controller_scope"] = new(Grant.AdminOnly, false),
            ["gitlab_list_runner_controller_tokens"] = new(Grant.AdminOnly, true),
            ["gitlab_rotate_runner_controller_token"] = new(Grant.AdminOnly, false),
            ["gitlab_revoke_runner_controller_token"] = new(Grant.AdminOnly, false),

            // IEnvironmentsClient / IDeploymentsClient / IProtectedEnvironmentsClient — project-scoped
            // and reachable by a DevOps-scoped token with the right project/group role, so these carry
            // the plain DevOps bit, never AdminOnly (same reasoning as the IRunnersClient block above).
            ["gitlab_list_environments"] = new(Grant.DevOps, true),
            ["gitlab_get_environment"] = new(Grant.DevOps, true),
            ["gitlab_update_environment"] = new(Grant.DevOps, false),
            ["gitlab_stop_environment"] = new(Grant.DevOps, false),
            ["gitlab_delete_environment"] = new(Grant.DevOps, false),
            ["gitlab_stop_stale_environments"] = new(Grant.DevOps, false),
            ["gitlab_delete_stale_review_apps"] = new(Grant.DevOps, false),
            ["gitlab_list_deployments"] = new(Grant.DevOps, true),
            ["gitlab_get_deployment"] = new(Grant.DevOps, true),
            ["gitlab_create_deployment"] = new(Grant.DevOps, false),
            ["gitlab_update_deployment_status"] = new(Grant.DevOps, false),
            ["gitlab_delete_deployment"] = new(Grant.DevOps, false),
            ["gitlab_approve_deployment"] = new(Grant.DevOps, false),
            ["gitlab_list_protected_environments"] = new(Grant.DevOps, true),
            ["gitlab_get_protected_environment"] = new(Grant.DevOps, true),
            ["gitlab_protect_environment"] = new(Grant.DevOps, false),
            ["gitlab_update_protected_environment"] = new(Grant.DevOps, false),
            ["gitlab_unprotect_environment"] = new(Grant.DevOps, false),

            // IFreezePeriodsClient — project-scoped and reachable by a DevOps-scoped token with the
            // right project role, so plain DevOps, never AdminOnly (same reasoning as IRunnersClient).
            ["gitlab_list_freeze_periods"] = new(Grant.DevOps, true),
            ["gitlab_get_freeze_period"] = new(Grant.DevOps, true),
            ["gitlab_create_freeze_period"] = new(Grant.DevOps, false),
            ["gitlab_update_freeze_period"] = new(Grant.DevOps, false),
            ["gitlab_delete_freeze_period"] = new(Grant.DevOps, false),

            // IReleasesClient — releases are Deploy's own read-mostly surface shared with Maintainer and
            // Developer per CLAUDE.md's Scope column; writes are Delivery (Developer+DevOps) or DevOps-only
            // per the backlog's own "profiles" list for each tool.
            ["gitlab_list_releases"] = new(Grant.Everyone, true),
            ["gitlab_get_release"] = new(Grant.Everyone, true),
            ["gitlab_update_release"] = new(Grant.Delivery, false),
            ["gitlab_delete_release"] = new(Grant.DevOps, false),
            ["gitlab_generate_release_evidence"] = new(Grant.DevOps, false),
            ["gitlab_list_release_links"] = new(Grant.Delivery, true),
            ["gitlab_get_release_link"] = new(Grant.DevOps, true),
            ["gitlab_create_release_link"] = new(Grant.Delivery, false),
            ["gitlab_update_release_link"] = new(Grant.DevOps, false),
            ["gitlab_delete_release_link"] = new(Grant.DevOps, false),
            ["gitlab_get_latest_release"] = new(Grant.Everyone, true),

            // backlog-split/deploy/part-4.json — download/deploy-key/deploy-token/secure-file/pages
            // rows. Every one of these is project-scoped and reachable by a DevOps-scoped token with
            // the right project role (GitLab's own 403 is the ceiling for a lower-privileged one), so
            // plain DevOps, never AdminOnly — same reasoning as the IRunnersClient/IEnvironmentsClient
            // blocks above. The chunk's own "profiles" field names DevOps for every one of these.
            ["gitlab_download_release_asset"] = new(Grant.DevOps, true),
            ["gitlab_list_deploy_keys"] = new(Grant.DevOps, true),
            ["gitlab_get_deploy_key"] = new(Grant.DevOps, true),
            ["gitlab_add_deploy_key"] = new(Grant.DevOps, false),
            ["gitlab_update_deploy_key"] = new(Grant.DevOps, false),
            ["gitlab_delete_deploy_key"] = new(Grant.DevOps, false),
            ["gitlab_enable_deploy_key"] = new(Grant.DevOps, false),
            ["gitlab_list_deploy_tokens"] = new(Grant.DevOps, true),
            ["gitlab_get_deploy_token"] = new(Grant.DevOps, true),
            ["gitlab_create_deploy_token"] = new(Grant.DevOps, false),
            ["gitlab_delete_deploy_token"] = new(Grant.DevOps, false),
            ["gitlab_upload_secure_file"] = new(Grant.DevOps, false),
            ["gitlab_get_secure_file"] = new(Grant.DevOps, true),
            ["gitlab_download_secure_file"] = new(Grant.DevOps, true),
            ["gitlab_delete_secure_file"] = new(Grant.DevOps, false),
            ["gitlab_get_pages_settings"] = new(Grant.DevOps, true),
            ["gitlab_update_pages_settings"] = new(Grant.DevOps, false),
            ["gitlab_unpublish_pages"] = new(Grant.DevOps, false),

            // backlog-split/deploy/part-5.json — IPagesClient's remaining project-scoped domain
            // management surface (as opposed to gitlab_list_instance_pages_domains's instance-wide
            // admin view above). Every method here is keyed by ProjectId and reachable by a
            // DevOps-scoped token with the right project role, so plain DevOps, never AdminOnly — same
            // reasoning as the IRunnersClient/IEnvironmentsClient blocks above. The chunk's own
            // "profiles" field names DevOps for every one of these.
            ["gitlab_check_pages_access"] = new(Grant.DevOps, true),
            ["gitlab_list_pages_domains"] = new(Grant.DevOps, true),
            ["gitlab_get_pages_domain"] = new(Grant.DevOps, true),
            ["gitlab_update_pages_domain"] = new(Grant.DevOps, false),
            ["gitlab_delete_pages_domain"] = new(Grant.DevOps, false),
            ["gitlab_verify_pages_domain"] = new(Grant.DevOps, false)
        };
    }
}