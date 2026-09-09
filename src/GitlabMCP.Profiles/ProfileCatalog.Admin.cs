using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Instance admin, hooks, integrations, audit tools (AdminTools.cs). A row carrying bare
    // Grant.AdminOnly is reachable only under McpProfile.FullPermission (DEC-005) -- never under a
    // persona profile alone. Grant.IsVisibleIn checks each bit independently (an OR of independent
    // flags, not an AND-gate), so putting a persona bit on the SAME row as AdminOnly does NOT mean
    // "that persona AND admin" -- it makes the row reachable under that persona ALONE, and the
    // AdminOnly bit becomes inert. Combine AdminOnly with a persona bit only when the tool is
    // genuinely meant to be directly usable by that persona profile; instance-administrator-only
    // tools (per their own [Description]) get bare Grant.AdminOnly instead.
    private static IReadOnlyDictionary<string, ToolGrant> AdminRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_get_instance_appearance"] = new(Grant.AdminOnly, true),
            ["gitlab_list_feature_flag_user_lists"] = new(Grant.Delivery, true),
            // Project/group-scoped -- no "instance administrator" language in their own [Description] --
            // so they keep their persona bit(s) and drop the inert AdminOnly bit.
            ["gitlab_list_license_policies"] = new(Grant.DevOps, true),
            ["gitlab_list_group_integrations"] = new(Grant.Delivery, true),
            // Instance-wide ("Requires instance administrator access" per its own [Description]) --
            // bare AdminOnly, matching gitlab_list_audit_events below.
            ["gitlab_list_background_migration_operations"] = new(Grant.AdminOnly, true),
            ["gitlab_list_audit_events"] = new(Grant.AdminOnly, true),
            ["gitlab_list_project_hook_deliveries"] = new(Grant.Delivery, true),
            ["gitlab_list_group_hook_deliveries"] = new(Grant.Delivery, true),
            ["gitlab_create_feature_flag"] = new(Grant.Delivery, false),
            ["gitlab_set_license_policy"] = new(Grant.DevOps, false),
            ["gitlab_create_project_hook"] = new(Grant.Delivery, false),
            ["gitlab_create_group_hook"] = new(Grant.Delivery, false),
            ["gitlab_create_system_hook"] = new(Grant.AdminOnly, false),
            ["gitlab_set_group_integration"] = new(Grant.Delivery, false),

            // Instance appearance/settings/statistics/plan-limits/audit/system-hooks -- every one of
            // these tools' own [Description] states "Requires instance administrator access"
            // unconditionally, so each gets bare Grant.AdminOnly.
            ["gitlab_update_instance_appearance"] = new(Grant.AdminOnly, false),
            ["gitlab_get_instance_settings"] = new(Grant.AdminOnly, true),
            ["gitlab_update_instance_settings"] = new(Grant.AdminOnly, false),
            ["gitlab_get_instance_statistics"] = new(Grant.AdminOnly, true),
            // GetMetadataAsync's own XML doc: "The only operation on this client that does not require
            // administrator rights" -- its [Description] says so too, so it keeps the bare DevOps
            // persona bit rather than AdminOnly (DEC-027).
            ["gitlab_get_instance_metadata"] = new(Grant.DevOps, true),
            ["gitlab_get_plan_limits"] = new(Grant.AdminOnly, true),
            ["gitlab_update_plan_limits"] = new(Grant.AdminOnly, false),
            ["gitlab_get_current_license"] = new(Grant.AdminOnly, true),
            ["gitlab_list_licenses"] = new(Grant.AdminOnly, true),
            ["gitlab_get_license"] = new(Grant.AdminOnly, true),
            ["gitlab_activate_license"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_license"] = new(Grant.AdminOnly, false),
            ["gitlab_refresh_license_billable_users"] = new(Grant.AdminOnly, false),
            // Project-scoped, same shape as gitlab_list_license_policies/gitlab_set_license_policy above
            // -- keeps the bare DevOps persona bit, no AdminOnly language in its own [Description].
            ["gitlab_delete_license_policy"] = new(Grant.DevOps, false),
            ["gitlab_get_audit_event"] = new(Grant.AdminOnly, true),
            // ListGroupAuditEventsAsync's own [Description]: "Requires the Owner role on the group, or
            // instance administrator access" -- unlike gitlab_get_audit_event/gitlab_list_audit_events
            // above (unconditional admin-only wording), this tool has a non-admin access path (group
            // Owner), so per DEC-027 it keeps the bare DevOps persona bit (CLAUDE.md maps AuditEvents to
            // DevOps) rather than AdminOnly, instead of being hidden from a persona whose GitLab role
            // would let them actually call it.
            ["gitlab_list_group_audit_events"] = new(Grant.DevOps, true),
            ["gitlab_list_system_hooks"] = new(Grant.AdminOnly, true),
            ["gitlab_get_system_hook"] = new(Grant.AdminOnly, true),

            // Instance-wide ("Requires instance administrator access" per its own [Description]) --
            // bare AdminOnly, matching gitlab_create_system_hook/gitlab_list_system_hooks above.
            ["gitlab_update_system_hook"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_system_hook"] = new(Grant.AdminOnly, false),
            ["gitlab_test_system_hook"] = new(Grant.AdminOnly, false),

            // Project/group-scoped webhooks and integrations -- no "instance administrator" language in
            // their own [Description] (same shape as gitlab_create_project_hook/gitlab_create_group_hook
            // above), so these keep their bare persona bit(s) and drop the inert AdminOnly bit (DEC-027).
            ["gitlab_list_project_hooks"] = new(Grant.Delivery, true),
            ["gitlab_get_project_hook"] = new(Grant.Delivery, true),
            ["gitlab_update_project_hook"] = new(Grant.Delivery, false),
            ["gitlab_delete_project_hook"] = new(Grant.Delivery, false),
            ["gitlab_test_project_hook"] = new(Grant.Delivery, false),
            ["gitlab_resend_project_hook_delivery"] = new(Grant.Delivery, false),
            ["gitlab_list_group_hooks"] = new(Grant.Delivery, true),
            ["gitlab_get_group_hook"] = new(Grant.Delivery, true),
            ["gitlab_update_group_hook"] = new(Grant.Delivery, false),
            ["gitlab_delete_group_hook"] = new(Grant.Delivery, false),
            ["gitlab_test_group_hook"] = new(Grant.Delivery, false),
            ["gitlab_resend_group_hook_delivery"] = new(Grant.Delivery, false),
            ["gitlab_list_project_integrations"] = new(Grant.Delivery, true),
            ["gitlab_get_project_integration"] = new(Grant.Delivery, true),
            ["gitlab_get_group_integration"] = new(Grant.Delivery, true),

            // ---- backlog-split/admin/part-3.json below ----

            // Project/group-scoped integration management -- same shape as gitlab_set_group_integration
            // above (no "instance administrator" language in their own [Description]), so these keep
            // their bare persona bit(s) and drop the backlog item's inert AdminOnly candidate (DEC-027).
            ["gitlab_set_project_integration"] = new(Grant.Delivery, false),
            ["gitlab_disable_project_integration"] = new(Grant.Delivery, false),
            ["gitlab_disable_group_integration"] = new(Grant.Delivery, false),

            // Project-scoped platform integrations (Google Cloud setup script, job-token audience
            // exchange) -- DevOps surface, no instance-administrator language in their own
            // [Description], so DevOps alone rather than DevOps | AdminOnly (DEC-027).
            ["gitlab_get_google_cloud_setup_script"] = new(Grant.DevOps, true),
            ["gitlab_exchange_platform_token"] = new(Grant.DevOps, false),
            ["gitlab_get_ci_job_allowed_agents"] = new(Grant.DevOps, true),

            // Instance-wide Flipper development flags (IFeaturesClient) -- genuinely
            // administrator-only per the client's own type doc ("administrator-only and
            // instance-wide"), and each tool's own [Description] says so -- bare Grant.AdminOnly.
            ["gitlab_list_instance_features"] = new(Grant.AdminOnly, true),
            ["gitlab_list_feature_definitions"] = new(Grant.AdminOnly, true),
            ["gitlab_set_instance_feature"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_instance_feature_gate"] = new(Grant.AdminOnly, false),

            // Project feature flags (IFeatureFlagsClient) -- same Delivery surface as
            // gitlab_create_feature_flag/gitlab_list_feature_flag_user_lists above.
            ["gitlab_list_feature_flags"] = new(Grant.Delivery, true),
            ["gitlab_get_feature_flag"] = new(Grant.Delivery, true),
            ["gitlab_update_feature_flag"] = new(Grant.Delivery, false),
            ["gitlab_delete_feature_flag"] = new(Grant.Delivery, false),
            ["gitlab_get_feature_flag_settings"] = new(Grant.Delivery, true),
            // Raising the minimum role can lock the caller out of further edits -- DevOps only, per
            // the backlog item's own narrower profile list (not the wider Delivery surface the other
            // feature-flag tools get).
            ["gitlab_update_feature_flag_settings"] = new(Grant.DevOps, false),
            ["gitlab_get_feature_flag_user_list"] = new(Grant.Delivery, true),
            ["gitlab_create_feature_flag_user_list"] = new(Grant.Delivery, false),

            // ---- backlog-split/admin/part-4.json below ----

            // Project-scoped feature flag user list writes (IFeatureFlagsClient) -- same Delivery
            // surface as gitlab_list_feature_flag_user_lists/gitlab_create_feature_flag_user_list above.
            ["gitlab_update_feature_flag_user_list"] = new(Grant.Delivery, false),
            ["gitlab_delete_feature_flag_user_list"] = new(Grant.Delivery, false),

            // Instance-wide OAuth application registration (IApplicationsClient) -- "the instance-wide
            // admin area (/applications)" per the client's own type doc, and each tool's own
            // [Description] states "Requires instance administrator access" -- bare Grant.AdminOnly.
            ["gitlab_list_oauth_applications"] = new(Grant.AdminOnly, true),
            ["gitlab_create_oauth_application"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_oauth_application"] = new(Grant.AdminOnly, false),

            // Per-user OAuth application self-service (IApplicationsClient's ...ForCurrentUserAsync
            // methods) -- "the per-user area that lets any user register their own OAuth application"
            // per the client's own type doc, no instance-administrator language in their own
            // [Description], so these keep their bare persona bit(s) and drop the backlog item's inert
            // AdminOnly candidate (DEC-027).
            ["gitlab_list_my_oauth_applications"] = new(Grant.Delivery, true),
            ["gitlab_create_my_oauth_application"] = new(Grant.Delivery, false),
            ["gitlab_delete_my_oauth_application"] = new(Grant.Delivery, false),

            // Instance-wide broadcast messages (IBroadcastMessagesClient) -- "every operation requires
            // the Administrator role" per the client's own type doc -- bare Grant.AdminOnly.
            ["gitlab_list_broadcast_messages"] = new(Grant.AdminOnly, true),
            ["gitlab_get_broadcast_message"] = new(Grant.AdminOnly, true),
            ["gitlab_create_broadcast_message"] = new(Grant.AdminOnly, false),
            ["gitlab_update_broadcast_message"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_broadcast_message"] = new(Grant.AdminOnly, false),

            // Sidekiq admin surface (ISidekiqClient) -- "every method here requires instance
            // administrator access" per the client's own type doc, so bare Grant.AdminOnly rather than
            // the backlog item's DevOps | AdminOnly candidate for the two read tools (DEC-027).
            ["gitlab_get_sidekiq_metrics"] = new(Grant.AdminOnly, true),
            ["gitlab_get_sidekiq_queue_metrics"] = new(Grant.AdminOnly, true),
            ["gitlab_delete_sidekiq_queue_jobs"] = new(Grant.AdminOnly, false),

            // Service Ping / usage telemetry (IUsageDataClient) -- "most of these endpoints require
            // administrator access" per the client's own type doc, and each tool's own [Description]
            // states "Requires instance administrator access" -- bare Grant.AdminOnly.
            ["gitlab_get_service_ping"] = new(Grant.AdminOnly, true),
            ["gitlab_get_service_ping_metric_definitions"] = new(Grant.AdminOnly, true),

            // ---- backlog-split/admin/part-5.json below ----

            // IUsageDataClient's own type doc: "Most of these endpoints require administrator access" --
            // matches the bare AdminOnly grant already used for its sibling Service Ping reads above.
            ["gitlab_track_usage_events"] = new(Grant.AdminOnly, false),

            // IBackgroundMigrationsClient's own type doc: "Every method here is available only to
            // instance administrators; GitLab answers 403 to anyone else." -- bare Grant.AdminOnly for
            // every tool on this client, matching gitlab_list_background_migration_operations above
            // (dropping the backlog item's inert DevOps candidate per DEC-027).
            ["gitlab_list_background_migrations"] = new(Grant.AdminOnly, true),
            ["gitlab_get_background_migration"] = new(Grant.AdminOnly, true),
            ["gitlab_control_background_migration"] = new(Grant.AdminOnly, false),
            ["gitlab_get_background_migration_operation"] = new(Grant.AdminOnly, true),
            ["gitlab_control_background_migration_operation"] = new(Grant.AdminOnly, false),

            // IAdminMigrationsClient's own type doc: "Every method here is available only to instance
            // administrators; GitLab answers 403 to anyone else." -- bare Grant.AdminOnly.
            ["gitlab_list_pending_migrations"] = new(Grant.AdminOnly, true),
            ["gitlab_mark_migration_applied"] = new(Grant.AdminOnly, false),

            // IInternalClient's own type doc carries no administrator-access language for the Swagger
            // description endpoints, and this tool's own [Description] states none either -- bare
            // DevOps persona bit, dropping the backlog item's inert AdminOnly candidate (DEC-027).
            ["gitlab_get_api_documentation"] = new(Grant.DevOps, true),

            // IComplianceSettingsClient.GetAsync/UpdateAsync -- both own [Description]s state "Requires
            // administrator access" (the instance-wide CSP namespace setting) -- bare Grant.AdminOnly.
            ["gitlab_get_compliance_settings"] = new(Grant.AdminOnly, true),
            ["gitlab_update_compliance_settings"] = new(Grant.AdminOnly, false),

            // IComplianceSettingsClient.SetExternalControlStatusAsync is project-scoped (a project's own
            // external compliance controls, not the instance-wide CSP namespace above) with no
            // administrator-access language in its own [Description] -- bare DevOps persona bit, dropping
            // the backlog item's inert AdminOnly candidate (DEC-027).
            ["gitlab_set_compliance_control_status"] = new(Grant.DevOps, false)
        };
    }
}