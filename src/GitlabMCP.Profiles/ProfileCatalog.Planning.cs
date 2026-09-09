using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Planning-domain tools (PlanningTools.cs): issues, milestones, boards, labels, iterations, todos,
    // resource groups and resource-event history. Per CLAUDE.md's persona table, "planning" work
    // (issues/tasks/milestones/boards/labels) is shared by Maintainer and Developer (Grant.Planning);
    // the two resource-group tools are CI/CD-adjacent and instead follow the "code, group and project
    // management that supports [MR/PR lifecycle]" line under Developer, plus DevOps (Grant.Delivery).
    private static IReadOnlyDictionary<string, ToolGrant> PlanningRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_issue_links"] = new(Grant.Planning, true),
            ["gitlab_list_milestone_burndown_events"] = new(Grant.Planning, true),
            ["gitlab_list_board_lists"] = new(Grant.Planning, true),
            ["gitlab_list_labels"] = new(Grant.Planning, true),
            ["gitlab_list_resource_groups"] = new(Grant.Delivery, true),
            ["gitlab_list_iterations"] = new(Grant.Planning, true),
            ["gitlab_list_todos"] = new(Grant.Planning, true),
            ["gitlab_list_resource_events"] = new(Grant.Planning, true),
            ["gitlab_list_issue_participants"] = new(Grant.Planning, true),
            ["gitlab_list_milestone_issues"] = new(Grant.Planning, true),
            ["gitlab_list_boards"] = new(Grant.Planning, true),
            ["gitlab_get_label"] = new(Grant.Planning, true),
            ["gitlab_create_issue"] = new(Grant.Planning, false),
            ["gitlab_create_board"] = new(Grant.Planning, false),
            ["gitlab_create_milestone"] = new(Grant.Planning, false),
            ["gitlab_create_label"] = new(Grant.Planning, false),
            ["gitlab_create_todo"] = new(Grant.Planning, false),
            ["gitlab_set_issue_subscription"] = new(Grant.Planning, false),
            ["gitlab_set_resource_group_process_mode"] = new(Grant.Delivery, false),
            ["gitlab_update_issue"] = new(Grant.Planning, false),
            ["gitlab_get_issue"] = new(Grant.Planning, true),
            ["gitlab_get_issue_by_global_id"] = new(Grant.Planning, true),
            ["gitlab_list_issues"] = new(Grant.Planning, true),
            ["gitlab_list_my_issues"] = new(Grant.Planning, true),
            ["gitlab_close_issue"] = new(Grant.Planning, false),
            ["gitlab_reopen_issue"] = new(Grant.Planning, false),
            ["gitlab_delete_issue"] = new(Grant.Planning, false),
            ["gitlab_move_issue"] = new(Grant.Planning, false),
            ["gitlab_clone_issue"] = new(Grant.Planning, false),
            ["gitlab_link_issues"] = new(Grant.Planning, false),
            ["gitlab_unlink_issues"] = new(Grant.Planning, false),
            ["gitlab_get_issue_time_stats"] = new(Grant.Planning, true),
            ["gitlab_manage_issue_time"] = new(Grant.Planning, false),
            ["gitlab_get_issue_statistics"] = new(Grant.Planning, true),
            ["gitlab_list_issue_related_merge_requests"] = new(Grant.Planning, true),
            ["gitlab_list_milestones"] = new(Grant.Planning, true),
            ["gitlab_get_milestone"] = new(Grant.Planning, true),
            ["gitlab_update_milestone"] = new(Grant.Planning, false),
            ["gitlab_delete_milestone"] = new(Grant.Planning, false),
            ["gitlab_promote_milestone"] = new(Grant.Planning, false),
            ["gitlab_list_milestone_merge_requests"] = new(Grant.Planning, true),
            ["gitlab_update_label"] = new(Grant.Planning, false),
            ["gitlab_delete_label"] = new(Grant.Planning, false),
            ["gitlab_promote_label"] = new(Grant.Planning, false),
            ["gitlab_get_board"] = new(Grant.Planning, true),
            ["gitlab_update_board"] = new(Grant.Planning, false),
            ["gitlab_delete_board"] = new(Grant.Planning, false),
            ["gitlab_create_board_list"] = new(Grant.Planning, false),
            ["gitlab_reorder_board_list"] = new(Grant.Planning, false),
            ["gitlab_delete_board_list"] = new(Grant.Planning, false),
            ["gitlab_mark_todo_done"] = new(Grant.Planning, false),
            ["gitlab_mark_all_todos_done"] = new(Grant.Planning, false),
            ["gitlab_set_merge_request_subscription"] = new(Grant.Planning, false),
            ["gitlab_set_label_subscription"] = new(Grant.Planning, false),
            // Resource groups are CI/CD-adjacent (see the two existing resource-group rows above),
            // so this follows Grant.Delivery (Developer | DevOps) per the chunk's own profile list,
            // not Grant.Planning.
            ["gitlab_get_resource_group"] = new(Grant.Delivery, true)
        };
    }
}