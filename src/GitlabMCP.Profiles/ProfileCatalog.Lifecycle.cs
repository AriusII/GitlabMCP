using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Import/export, ML experiment tracking and Duo Chat tools ("lifecycle" domain).
    //
    // None of this domain's tools are instance-administrator-only (none of their [Description]s say
    // "requires administrator access" — these are project/group-scoped migration and export operations
    // a DevOps operator runs directly). The bare Grant.DevOps below is correct and sufficient:
    // Grant.IsVisibleIn checks each bit independently, so DevOps alone already makes a row reachable
    // under both McpProfile.DevOps and McpProfile.FullPermission ("grant != Grant.None") — combining it
    // with Grant.AdminOnly would add nothing and, per DECISIONS.md's review-findings note (18+26), is
    // the exact pattern that silently defeats an intended admin-only restriction elsewhere in this
    // catalog. Reserve Grant.AdminOnly (bare, alone) for a tool whose own [Description] actually says
    // "requires administrator access" — none of this domain's tools do.
    private static IReadOnlyDictionary<string, ToolGrant> LifecycleRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_duo_chat"] = new(Grant.Everyone, true),
            ["gitlab_list_project_templates"] = new(Grant.Everyone, true),
            ["gitlab_list_bulk_import_entities"] = new(Grant.DevOps, true),
            ["gitlab_list_ml_experiments"] = new(Grant.DevOps, true),
            ["gitlab_get_group_export_download_info"] = new(Grant.DevOps, true),
            ["gitlab_create_ml_experiment"] = new(Grant.DevOps, false),
            ["gitlab_export_project"] = new(Grant.DevOps, false),
            ["gitlab_create_bulk_import"] = new(Grant.DevOps, false),

            // Backlog part-1: project/group import-export polling and control, direct-transfer (bulk
            // import) lifecycle, and MLflow run tracking. Same reasoning as above -- none of these
            // require instance-administrator rights (project export/import needs Maintainer+ on the
            // project; bulk import needs Owner on the destination namespace; ML runs need at least
            // Reporter on the project) -- so each keeps its bare persona bit(s) and drops the backlog's
            // listed AdminOnly bit as inert/harmful per DEC-027.
            ["gitlab_get_project_export_status"] = new(Grant.DevOps, true),
            ["gitlab_import_project_from_git"] = new(Grant.DevOps, false),
            ["gitlab_import_project_from_github"] = new(Grant.DevOps, false),
            ["gitlab_get_project_import_status"] = new(Grant.DevOps, true),
            ["gitlab_get_project_template"] = new(Grant.Everyone, true),
            ["gitlab_export_group"] = new(Grant.DevOps, false),
            ["gitlab_get_group_relations_export_status"] = new(Grant.DevOps, true),
            ["gitlab_list_bulk_imports"] = new(Grant.DevOps, true),
            ["gitlab_get_bulk_import"] = new(Grant.DevOps, true),
            ["gitlab_cancel_bulk_import"] = new(Grant.DevOps, false),
            ["gitlab_list_bulk_import_entity_failures"] = new(Grant.DevOps, true),
            ["gitlab_import_github_gists"] = new(Grant.Delivery, false),
            ["gitlab_get_ml_experiment"] = new(Grant.DevOps, true),
            ["gitlab_create_ml_run"] = new(Grant.DevOps, false),
            ["gitlab_get_ml_run"] = new(Grant.DevOps, true),
            ["gitlab_search_ml_runs"] = new(Grant.DevOps, true),
            ["gitlab_log_ml_run_data"] = new(Grant.DevOps, false),
            ["gitlab_update_ml_run"] = new(Grant.DevOps, false),

            // Backlog part-2: ML model registry, Duo Chat/Code-Suggestions/GLQL/Agent-Platform-workflow
            // tools, external coding-agent identities/sessions, and the instance A/B experiment
            // framework.
            //
            // Four rows below (gitlab_duo_check_code_suggestions_enabled, gitlab_start_duo_workflow,
            // gitlab_get_duo_workflow, gitlab_resume_duo_workflow) arrived from the backlog tagged
            // ["DevOps","AdminOnly"]. None of their own [Description]s say "requires administrator
            // access" -- each operates on a caller-supplied project/namespace/flow that any DevOps-
            // profile token with Duo access can already reach, not instance-wide configuration -- so
            // per DEC-027 they keep bare Grant.DevOps and drop the inert/harmful AdminOnly bit, same
            // reasoning as the part-1 comment above.
            //
            // The three gitlab_*experiment* rows are the opposite case: the backlog lists ONLY
            // "AdminOnly" for them (no persona bit at all), and IExperimentsClient.ListAsync takes no
            // project/group scoping parameter whatsoever -- this is GitLab's own internal A/B-testing
            // framework, genuinely instance-wide. Their own [Description]s say "Requires administrator
            // access", so they keep bare Grant.AdminOnly as the backlog intended.
            ["gitlab_list_ml_models"] = new(Grant.DevOps, true),
            ["gitlab_get_ml_model_version"] = new(Grant.DevOps, true),
            ["gitlab_create_ml_model"] = new(Grant.DevOps, false),
            ["gitlab_delete_ml_model"] = new(Grant.DevOps, false),
            ["gitlab_duo_generate_code_completion"] = new(Grant.Delivery, true),
            ["gitlab_duo_generate_git_command"] = new(Grant.Delivery, true),
            ["gitlab_duo_execute_glql_query"] = new(Grant.Everyone, true),
            ["gitlab_duo_check_code_suggestions_enabled"] = new(Grant.DevOps, true),
            ["gitlab_start_duo_workflow"] = new(Grant.DevOps, false),
            ["gitlab_get_duo_workflow"] = new(Grant.DevOps, true),
            ["gitlab_resume_duo_workflow"] = new(Grant.DevOps, false),
            ["gitlab_register_ai_agent_identity"] = new(Grant.Delivery, false),
            ["gitlab_list_ai_agent_sessions"] = new(Grant.Delivery, true),
            ["gitlab_create_ai_agent_session"] = new(Grant.Delivery, false),
            ["gitlab_complete_ai_agent_session"] = new(Grant.Delivery, false),
            ["gitlab_list_experiments"] = new(Grant.AdminOnly, true),
            ["gitlab_get_experiment_assignment"] = new(Grant.AdminOnly, true),
            ["gitlab_force_experiment_assignment"] = new(Grant.AdminOnly, false),

            // Backlog part-3: internal data-model/database-dictionary inspection (IDataManagementClient),
            // AutoFlow CD-rollout event ingestion, GitLab-for-Jira (Forge) installation management, and
            // mobile push-notification subscriptions.
            //
            // gitlab_list_admin_model_records, gitlab_get_database_dictionary_table,
            // gitlab_list_jira_forge_subscriptions and gitlab_delete_jira_forge_subscription are genuinely
            // instance-wide: none of IDataManagementClient's or IJiraConnectClient's methods here take a
            // ProjectId/GroupId -- there is no caller-owned scope to narrow to, matching the same
            // reasoning already recorded above for the three gitlab_*experiment* rows. Their own
            // [Description]s say "Requires administrator access", so they keep bare Grant.AdminOnly.
            //
            // gitlab_register_mobile_push_subscription and gitlab_unregister_mobile_push_subscription
            // arrived from the backlog tagged ["AdminOnly"], but that is wrong on the operations'
            // own terms: IMobilePushSubscriptionsClient's own XML docs say these act on "the calling
            // user's device token" / "the calling user's own push subscription" -- a personal
            // notification-delivery preference, not instance configuration, and nothing here is scoped
            // to any project/group/instance setting either. Per DEC-027, AdminOnly is dropped and their
            // own [Description]s make no "administrator access" claim; granted Grant.Everyone instead,
            // matching this file's existing treatment of gitlab_duo_chat and gitlab_get_project_template
            // as basic account-level actions useful to every persona.
            ["gitlab_list_admin_model_records"] = new(Grant.AdminOnly, true),
            ["gitlab_get_database_dictionary_table"] = new(Grant.AdminOnly, true),
            ["gitlab_ingest_rollout_event"] = new(Grant.DevOps, false),
            ["gitlab_list_jira_forge_subscriptions"] = new(Grant.AdminOnly, true),
            ["gitlab_delete_jira_forge_subscription"] = new(Grant.AdminOnly, false),
            ["gitlab_register_mobile_push_subscription"] = new(Grant.Everyone, false),
            ["gitlab_unregister_mobile_push_subscription"] = new(Grant.Everyone, false)
        };
    }
}