using GitLab.Client.Models;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Mapping.Cicd;

/// <summary>Projects the two shapes a job-token allowlist entry can take into one owned record.</summary>
public static class JobTokenScopeMapper
{
    public static AllowlistEntrySummary ToSummary(GitLabProject project)
    {
        return new AllowlistEntrySummary(
            project.Id,
            project.Name,
            project.PathWithNamespace,
            project.WebUrl?.ToString());
    }

    public static AllowlistEntrySummary ToSummary(GitLabJobTokenScopeGroup group)
    {
        return new AllowlistEntrySummary(
            group.Id,
            group.Name,
            null,
            group.WebUrl?.ToString());
    }
}