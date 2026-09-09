using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Repository/files/branches/commits tools (domain "code"). Profiles taken from
    // docs/tool-catalog-by-domain.json's "code" array, mapped onto Grant flags per CLAUDE.md's
    // Maintainer/Developer/DevOps persona tables (read-mostly on code for Maintainer, full lifecycle for
    // Developer, protection/administration for DevOps).
    private static IReadOnlyDictionary<string, ToolGrant> CodeRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_commit_comments"] = new(Grant.Planning, true),
            ["gitlab_list_repository_contributors"] = new(Grant.Planning, true),
            ["gitlab_get_file_blame"] = new(Grant.Planning, true),
            ["gitlab_list_branches"] = new(Grant.Planning, true),
            ["gitlab_list_tags"] = new(Grant.Planning, true),
            ["gitlab_list_commit_statuses"] = new(Grant.Delivery, true),
            ["gitlab_list_protected_branches"] = new(Grant.Everyone, true),
            ["gitlab_list_protected_tags"] = new(Grant.Everyone, true),
            ["gitlab_get_push_rule"] = new(Grant.Delivery, true),
            ["gitlab_get_pull_mirror"] = new(Grant.Delivery, true),
            ["gitlab_create_commit"] = new(Grant.Developer, false),
            ["gitlab_create_file"] = new(Grant.Developer, false),
            ["gitlab_create_branch"] = new(Grant.Developer, false),
            ["gitlab_create_tag"] = new(Grant.Developer, false),
            ["gitlab_protect_branch"] = new(Grant.DevOps, false),
            ["gitlab_protect_tag"] = new(Grant.DevOps, false),

            // Backlog chunk code/part-1: repository tree/files, commit reads and lifecycle writes,
            // branch/tag deletion, ref comparison, and the two DevOps-only writes (commit status,
            // unprotect). Profiles taken from the chunk's own "profiles" array.
            ["gitlab_list_repository_tree"] = new(Grant.Planning, true),
            ["gitlab_get_file_content"] = new(Grant.Planning, true),
            ["gitlab_update_file"] = new(Grant.Developer, false),
            ["gitlab_delete_file"] = new(Grant.Developer, false),
            ["gitlab_list_commits"] = new(Grant.Planning, true),
            ["gitlab_get_commit"] = new(Grant.Planning, true),
            ["gitlab_get_commit_diff"] = new(Grant.Planning, true),
            ["gitlab_cherry_pick_commit"] = new(Grant.Developer, false),
            ["gitlab_revert_commit"] = new(Grant.Developer, false),
            ["gitlab_comment_on_commit"] = new(Grant.Developer, false),
            ["gitlab_list_commit_merge_requests"] = new(Grant.Developer, true),
            ["gitlab_get_branch"] = new(Grant.Planning, true),
            ["gitlab_delete_branch"] = new(Grant.Developer, false),
            ["gitlab_delete_merged_branches"] = new(Grant.Developer, false),
            ["gitlab_delete_tag"] = new(Grant.Developer, false),
            ["gitlab_compare_refs"] = new(Grant.Planning, true),
            ["gitlab_set_commit_status"] = new(Grant.DevOps, false),
            ["gitlab_unprotect_branch"] = new(Grant.DevOps, false),

            // Backlog chunk code/part-2: tag unprotection, project push-rule create/update, and push
            // mirror listing/creation. Profiles taken from the chunk's own "profiles" array.
            ["gitlab_unprotect_tag"] = new(Grant.DevOps, false),
            ["gitlab_create_push_rule"] = new(Grant.DevOps, false),
            ["gitlab_update_push_rule"] = new(Grant.DevOps, false),
            ["gitlab_list_push_mirrors"] = new(Grant.Delivery, true),
            ["gitlab_create_push_mirror"] = new(Grant.DevOps, false)
        };
    }
}