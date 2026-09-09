using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>
///     Projects <see cref="GitLabPackageProtectionRule" /> and <see cref="GitLabContainerRegistryProtectionRule" />
///     into their owned summary records.
/// </summary>
public static class ProtectionRuleMapper
{
    public static PackageProtectionRuleSummary ToSummary(GitLabPackageProtectionRule rule)
    {
        return new PackageProtectionRuleSummary(
            rule.Id,
            rule.ProjectId,
            rule.PackageNamePattern,
            rule.PackageType,
            rule.MinimumAccessLevelForPush,
            rule.MinimumAccessLevelForDelete);
    }

    public static ContainerRegistryProtectionRuleSummary ToSummary(GitLabContainerRegistryProtectionRule rule)
    {
        return new ContainerRegistryProtectionRuleSummary(
            rule.Id,
            rule.ProjectId,
            rule.RepositoryPathPattern,
            rule.MinimumAccessLevelForPush,
            rule.MinimumAccessLevelForDelete);
    }

    public static ContainerRegistryProtectionTagRuleSummary ToSummary(GitLabContainerRegistryProtectionTagRule rule)
    {
        return new ContainerRegistryProtectionTagRuleSummary(
            rule.Id,
            rule.ProjectId,
            rule.TagNamePattern,
            rule.MinimumAccessLevelForPush,
            rule.MinimumAccessLevelForDelete);
    }
}