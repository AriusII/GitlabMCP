using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     One starter prompt per persona profile (CLAUDE.md: "each profile should ship its own
///     ServerInstructions and [McpServerPrompt] set"). Plain <c>string</c> return and no arguments — no
///     JsonSerializable entry needed (mcp-prompts-and-resources §Step 2/3). Bodies are static literals, per
///     mcp-untrusted-content §Step 2 — nothing interpolated.
/// </summary>
[McpServerPromptType]
public sealed class ProfilePrompts
{
    [McpServerPrompt(Name = "gitlab_maintainer_guide")]
    [Description("How to use this server's planning tools: issues, milestones, iterations, labels, boards, and epics.")]
    public string MaintainerGuide()
    {
        return "Use gitlab_list_issues / gitlab_get_epic / gitlab_list_epics to survey open work before creating "
               + "anything new. Prefer updating an existing issue or epic over creating a duplicate. Epics and "
               + "issues both support labels, milestones and iterations — set them so the board and roadmap stay "
               + "accurate. This server cannot merge code or run CI; hand code changes to a Developer-profile "
               + "session.";
    }

    [McpServerPrompt(Name = "gitlab_developer_guide")]
    [Description(
        "How to use this server's merge-request and code tools: branches, commits, files, reviews, pipelines.")]
    public string DeveloperGuide()
    {
        return "Check gitlab_list_merge_request_pipelines before merging — a red pipeline should block a merge "
               + "even if the diff looks correct. Use gitlab_create_branch from the target branch's latest commit, "
               + "never blind. Read existing discussions on a merge request before adding a note, to avoid "
               + "repeating a reviewer's point. Epics and issues are read/write here too, for linking code to the "
               + "work it implements.";
    }

    [McpServerPrompt(Name = "gitlab_devops_guide")]
    [Description(
        "How to use this server's CI/CD and infrastructure tools: pipelines, runners, variables, registries, environments.")]
    public string DevOpsGuide()
    {
        return "Prefer gitlab_validate_ci_config before gitlab_create_pipeline when a .gitlab-ci.yml change is "
               + "involved — a syntax error caught before triggering a pipeline saves a wasted runner minute. "
               + "Treat every CI variable as a potential secret even when it is not marked protected. Issues and "
               + "merge requests are readable here for context only; use a Developer-profile session to act on them.";
    }
}