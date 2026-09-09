using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

/// <summary>
///     The per-profile system message (CLAUDE.md: "each profile should ship its own ServerInstructions").
///     Critical Rule enforced structurally: one <c>const</c> per profile, nothing interpolated, no way for a
///     request value or GitLab-returned text to reach it (mcp-untrusted-content Step 2).
/// </summary>
public static class ProfileInstructions
{
    private const string Maintainer =
        "This server exposes planning tools for a GitLab project or group: issues, milestones, "
        + "iterations, labels, boards, epics/work items, wikis, and read access to code and merge "
        + "requests. Use it to triage and organise work, not to change code or run CI. Writing "
        + "code, merging, and pipeline/infrastructure administration are absent.";

    private const string Developer =
        "This server exposes the merge request lifecycle, code, branches, commits, epics/work items, "
        + "issues, and the project/group management that supports them, plus read access to pipelines. "
        + "Use it to move code through review and understand why a pipeline failed. Runner, environment "
        + "and instance administration are absent.";

    private const string DevOps =
        "This server exposes CI/CD, runners, environments, deployments, variables, package/container "
        + "registries, and infrastructure administration for a GitLab project or group. Use it to "
        + "operate pipelines and infrastructure. Issues and merge requests are readable for context "
        + "only; nothing here creates or reviews code changes.";

    private const string FullPermission =
        "This server exposes the entire GitLab surface, including instance administration. It is an "
        + "administrative and debugging profile: prefer a narrower one for routine work. Every write "
        + "here is still bounded by the configured token's own role and scope.";

    public static string For(McpProfile profile)
    {
        return profile switch
        {
            McpProfile.Maintainer => Maintainer,
            McpProfile.Developer => Developer,
            McpProfile.DevOps => DevOps,
            McpProfile.FullPermission => FullPermission,
            _ => throw new InvalidOperationException($"McpProfile.{profile} has no arm in ProfileInstructions.For.")
        };
    }
}