using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class EnvironmentMapper
{
    public static EnvironmentSummary ToSummary(GitLabEnvironment environment)
    {
        return new EnvironmentSummary(
            environment.Id,
            environment.Name,
            environment.Slug,
            environment.ExternalUrl?.ToString(),
            environment.State,
            environment.Tier,
            environment.Description);
    }
}