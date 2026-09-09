using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Notes, discussions & reactions (CLAUDE.md persona tables list "issues, tasks" discussion
    // sub-resources under both Maintainer and Developer for the read/react/comment surface; draft
    // notes, review-comment publishing and suggestion application are Developer-only MR review tools).
    private static IReadOnlyDictionary<string, ToolGrant> DiscussionRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_discussions"] = new(Grant.Planning, true),
            ["gitlab_list_notes"] = new(Grant.Planning, true),
            ["gitlab_list_draft_notes"] = new(Grant.Developer, true),
            ["gitlab_list_reactions"] = new(Grant.Planning, true),
            ["gitlab_render_markdown"] = new(Grant.Planning, true),
            ["gitlab_list_events"] = new(Grant.Planning, true),
            ["gitlab_get_discussion"] = new(Grant.Planning, true),
            ["gitlab_list_discussion_notes"] = new(Grant.Planning, true),
            ["gitlab_create_discussion"] = new(Grant.Planning, false),
            ["gitlab_create_draft_note"] = new(Grant.Developer, false),
            ["gitlab_update_note"] = new(Grant.Planning, false),
            ["gitlab_add_reaction"] = new(Grant.Planning, false),
            ["gitlab_apply_suggestion"] = new(Grant.Developer, false),

            ["gitlab_get_note"] = new(Grant.Planning, true),
            ["gitlab_add_note"] = new(Grant.Planning, false),
            ["gitlab_delete_note"] = new(Grant.Planning, false),
            ["gitlab_resolve_discussion"] = new(Grant.Planning, false),
            ["gitlab_reply_to_discussion"] = new(Grant.Planning, false),
            ["gitlab_get_discussion_note"] = new(Grant.Planning, true),
            ["gitlab_update_discussion_note"] = new(Grant.Planning, false),
            ["gitlab_resolve_discussion_note"] = new(Grant.Developer, false),
            ["gitlab_delete_discussion_note"] = new(Grant.Planning, false),
            ["gitlab_remove_reaction"] = new(Grant.Planning, false),
            ["gitlab_get_draft_note"] = new(Grant.Developer, true),
            ["gitlab_update_draft_note"] = new(Grant.Developer, false),
            ["gitlab_delete_draft_note"] = new(Grant.Developer, false),
            ["gitlab_publish_draft_note"] = new(Grant.Developer, false),
            ["gitlab_publish_all_draft_notes"] = new(Grant.Developer, false),
            ["gitlab_apply_suggestions_batch"] = new(Grant.Developer, false)
        };
    }
}