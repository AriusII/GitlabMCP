using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>Projects <see cref="GitLabDebianDistribution" /> into the owned <see cref="DebianDistributionSummary" />.</summary>
public static class DebianMapper
{
    public static DebianDistributionSummary ToSummary(GitLabDebianDistribution distribution)
    {
        return new DebianDistributionSummary(
            distribution.Id,
            distribution.Codename,
            distribution.Suite,
            distribution.Origin,
            distribution.Label,
            distribution.Version,
            distribution.Description,
            distribution.Components ?? [],
            distribution.Architectures ?? []);
    }
}