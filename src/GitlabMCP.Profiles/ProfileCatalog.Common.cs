using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // The canary tool (GitlabMCP.Tools/PingTools.cs) — visible in every profile, no GitLab dependency.
    private static IReadOnlyDictionary<string, ToolGrant> CommonRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_ping"] = new(Grant.Full, true)
        };
    }

    // Filled in as GitlabMCP.Tools' [McpServerPrompt] set grows (CLAUDE.md: "each profile should ship
    // its own ServerInstructions and [McpServerPrompt] set"). A row here with no matching registered
    // prompt fails AssertCatalogIsComplete by design — add the row and the prompt in the same commit.
    private static IReadOnlyDictionary<string, PrimitiveGrant> PromptRows()
    {
        return new Dictionary<string, PrimitiveGrant>(StringComparer.Ordinal)
        {
            ["gitlab_maintainer_guide"] = new(Grant.Maintainer),
            ["gitlab_developer_guide"] = new(Grant.Developer),
            ["gitlab_devops_guide"] = new(Grant.DevOps),

            // DEC-030: task-oriented prompts (GitlabMCP.Tools/TaskPrompts.cs), granted to match the same
            // persona(s) that already hold the tools each one walks through.
            ["gitlab_triage_issue_backlog"] = new(Grant.Planning),
            ["gitlab_review_merge_request"] = new(Grant.Developer),
            ["gitlab_plan_iteration"] = new(Grant.Maintainer),
            ["gitlab_investigate_pipeline_failure"] = new(Grant.DevOps),
            ["gitlab_prepare_release"] = new(Grant.Delivery),
            ["gitlab_audit_access_review"] = new(Grant.DevOps)
        };
    }
}