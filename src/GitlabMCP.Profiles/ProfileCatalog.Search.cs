using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Search/analytics/wiki/snippets/templates, plus the Zoekt/Knowledge-Graph/ActiveContext admin
    // surfaces that live on the same resource clients (domain "search").
    private static IReadOnlyDictionary<string, ToolGrant> SearchRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_search_migrations"] = new(Grant.AdminOnly, true),
            ["gitlab_list_all_snippets"] = new(Grant.AdminOnly, true),
            ["gitlab_list_license_templates"] = new(Grant.Planning, true),
            ["gitlab_list_code_review_analytics"] = new(Grant.Developer, true),
            ["gitlab_list_wiki_pages"] = new(Grant.Planning, true),
            // The Zoekt/Knowledge-Graph/ActiveContext admin rows below (interleaved with ordinary
            // Planning/Developer rows) are all instance-administrator-only per their own [Description]
            // text -- Grant.DevOps would make them reachable under a bare DevOps profile too, since
            // Grant.IsVisibleIn checks each bit independently (an OR, not an AND with AdminOnly), so the
            // DevOps bit is dropped rather than combined with AdminOnly.
            ["gitlab_list_code_search_indexed_namespaces"] = new(Grant.AdminOnly, true),
            ["gitlab_list_knowledge_graph_namespaces"] = new(Grant.AdminOnly, true),
            ["gitlab_update_active_context_collection"] = new(Grant.AdminOnly, false),
            ["gitlab_create_wiki_page"] = new(Grant.Planning, false),
            ["gitlab_add_code_search_indexed_namespace"] = new(Grant.AdminOnly, false),
            ["gitlab_create_snippet"] = new(Grant.Developer, false),
            ["gitlab_disable_knowledge_graph_namespace"] = new(Grant.AdminOnly, false),
            ["gitlab_search_projects"] = new(Grant.Planning, true),
            ["gitlab_search_issues"] = new(Grant.Planning, true),
            ["gitlab_search_merge_requests"] = new(Grant.Planning, true),
            ["gitlab_search_users"] = new(Grant.Planning, true),
            ["gitlab_search_milestones"] = new(Grant.Planning, true),
            ["gitlab_search_notes"] = new(Grant.Planning, true),
            ["gitlab_search_commits"] = new(Grant.Developer, true),
            ["gitlab_search_semantic_code"] = new(Grant.Developer, true),
            // Admin-only per its own [Description] ("Requires an administrator token") -- bare
            // Grant.AdminOnly, never combined with a persona bit (DEC-027).
            ["gitlab_get_search_migration"] = new(Grant.AdminOnly, true),
            ["gitlab_get_wiki_page"] = new(Grant.Planning, true),
            ["gitlab_update_wiki_page"] = new(Grant.Planning, false),
            ["gitlab_delete_wiki_page"] = new(Grant.Planning, false),
            ["gitlab_upload_wiki_attachment"] = new(Grant.Planning, false),
            ["gitlab_list_snippets"] = new(Grant.Developer, true),
            ["gitlab_list_public_snippets"] = new(Grant.Developer, true),
            ["gitlab_get_snippet"] = new(Grant.Developer, true),
            ["gitlab_update_snippet"] = new(Grant.Developer, false),
            ["gitlab_delete_snippet"] = new(Grant.Developer, false),

            // --- backlog chunk part-2 additions below ---
            ["gitlab_get_snippet_content"] = new(Grant.Developer, true),
            ["gitlab_get_snippet_file_content"] = new(Grant.Developer, true),
            // Admin-only per its own [Description] ("Requires administrator access") -- bare
            // Grant.AdminOnly, never combined with a persona bit (DEC-027).
            ["gitlab_get_snippet_user_agent_detail"] = new(Grant.AdminOnly, true),
            ["gitlab_list_templates"] = new(Grant.Planning, true),
            ["gitlab_get_template"] = new(Grant.Planning, true),
            ["gitlab_get_license_template"] = new(Grant.Planning, true),
            ["gitlab_get_group_activity_summary"] = new(Grant.Maintainer, true),
            ["gitlab_list_deployment_frequency"] = new(Grant.Everyone, true),
            ["gitlab_get_dora_metrics"] = new(Grant.Everyone, true),
            // The Zoekt/Knowledge-Graph/ActiveContext admin rows below are all instance-administrator-only
            // per their own [Description] text -- Grant.DevOps would make them reachable under a bare
            // DevOps profile too, since Grant.IsVisibleIn checks each bit independently (an OR, not an AND
            // with AdminOnly), so the DevOps bit is dropped rather than combined with AdminOnly (DEC-027).
            ["gitlab_list_code_search_shards"] = new(Grant.AdminOnly, true),
            ["gitlab_remove_code_search_indexed_namespace"] = new(Grant.AdminOnly, false),
            ["gitlab_update_code_search_namespace_replicas"] = new(Grant.AdminOnly, false),
            ["gitlab_trigger_code_search_indexing"] = new(Grant.AdminOnly, false),
            ["gitlab_enable_knowledge_graph_namespace"] = new(Grant.AdminOnly, false),
            ["gitlab_list_active_context_connections"] = new(Grant.AdminOnly, true),
            ["gitlab_activate_active_context_connection"] = new(Grant.AdminOnly, false),
            ["gitlab_deactivate_active_context_connection"] = new(Grant.AdminOnly, false),
            ["gitlab_update_active_context_namespace_state"] = new(Grant.AdminOnly, false),

            // --- backlog chunk part-3 additions below ---
            // Both ActiveContext dead-queue recovery actions are instance-administrator-only per their
            // own [Description] text; gitlab_replay_active_context_dead_queue's backlog entry also names
            // DevOps, but Grant.IsVisibleIn checks each bit independently (an OR, not an AND with
            // AdminOnly), so combining them would make it reachable under a bare DevOps profile too --
            // bare Grant.AdminOnly only (DEC-027).
            ["gitlab_clear_active_context_dead_queue"] = new(Grant.AdminOnly, false),
            ["gitlab_replay_active_context_dead_queue"] = new(Grant.AdminOnly, false)
        };
    }
}