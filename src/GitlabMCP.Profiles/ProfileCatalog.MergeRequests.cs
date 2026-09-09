using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Merge request lifecycle & approval-governance tools (CLAUDE.md's Developer persona table:
    // "MR/PR lifecycle ... and the code/group/project management that supports them"). The two tools
    // that also touch merge trains (a DevOps/Platform concern per the DevOps persona's CI/CD scope) are
    // granted Grant.Delivery (Developer|DevOps) instead of Grant.Developer alone.
    private static IReadOnlyDictionary<string, ToolGrant> MergeRequestsRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_group_merge_requests"] = new(Grant.Developer, true),
            ["gitlab_get_approval_settings"] = new(Grant.Developer, true),
            ["gitlab_list_approval_rules"] = new(Grant.Developer, true),
            ["gitlab_list_merge_train"] = new(Grant.Delivery, true),
            ["gitlab_list_external_status_checks"] = new(Grant.Delivery, true),
            ["gitlab_list_merge_request_commits"] = new(Grant.Developer, true),
            ["gitlab_get_merge_request_approvals"] = new(Grant.Developer, true),
            ["gitlab_list_merge_request_pipelines"] = new(Grant.Developer, true),
            ["gitlab_list_merge_request_related_issues"] = new(Grant.Developer, true),
            ["gitlab_list_merge_request_reviewers"] = new(Grant.Developer, true),
            ["gitlab_list_merge_requests"] = new(Grant.Developer, true),
            ["gitlab_create_merge_request"] = new(Grant.Developer, false),
            ["gitlab_create_approval_rule"] = new(Grant.Developer, false),
            ["gitlab_approve_merge_request"] = new(Grant.Developer, false),
            ["gitlab_add_to_merge_train"] = new(Grant.Delivery, false),
            ["gitlab_update_merge_request"] = new(Grant.Developer, false),
            ["gitlab_update_approval_rule"] = new(Grant.Developer, false),
            ["gitlab_unapprove_merge_request"] = new(Grant.Developer, false),
            ["gitlab_list_my_merge_requests"] = new(Grant.Developer, true),
            ["gitlab_get_merge_request"] = new(Grant.Developer, true),
            ["gitlab_get_merge_request_diff"] = new(Grant.Developer, true),
            ["gitlab_merge_merge_request"] = new(Grant.Developer, false),
            ["gitlab_delete_merge_request"] = new(Grant.Developer, false),
            ["gitlab_rebase_merge_request"] = new(Grant.Developer, false),
            ["gitlab_cancel_merge_when_pipeline_succeeds"] = new(Grant.Developer, false),
            ["gitlab_run_merge_request_pipeline"] = new(Grant.Developer, false),
            ["gitlab_delete_approval_rule"] = new(Grant.Developer, false)
        };
    }
}