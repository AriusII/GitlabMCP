namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     A project's package protection rule (<c>GitLabPackageProtectionRule</c>) — which package name pattern,
///     by format, only roles at or above the stated levels may push or delete. The pattern is
///     caller/maintainer-authored text round-tripped through GitLab, so every tool returning this wraps via
///     <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record PackageProtectionRuleSummary(
    long Id,
    long? ProjectId,
    string? PackageNamePattern,
    string? PackageType,
    string? MinimumAccessLevelForPush,
    string? MinimumAccessLevelForDelete);

public sealed record PackageProtectionRuleListResult(IReadOnlyList<PackageProtectionRuleSummary> Rules, bool Truncated);

/// <summary>
///     A project's container repository protection rule (<c>GitLabContainerRegistryProtectionRule</c>) — which
///     image repository path pattern only roles at or above the stated levels may push to or delete from.
/// </summary>
public sealed record ContainerRegistryProtectionRuleSummary(
    long Id,
    long? ProjectId,
    string? RepositoryPathPattern,
    string? MinimumAccessLevelForPush,
    string? MinimumAccessLevelForDelete);

public sealed record ContainerRegistryProtectionRuleListResult(
    IReadOnlyList<ContainerRegistryProtectionRuleSummary> Rules,
    bool Truncated);

/// <summary>All-scalar confirmation of a delete — no GitLab-authored string, so the tool returns this bare (unwrapped).</summary>
public sealed record PackageProtectionRuleDeleteResult(long RuleId, bool Deleted);

/// <summary>All-scalar confirmation of a delete — no GitLab-authored string, so the tool returns this bare (unwrapped).</summary>
public sealed record ContainerRegistryProtectionRuleDeleteResult(long RuleId, bool Deleted);

/// <summary>
///     A project's container repository *tag* protection rule (<c>GitLabContainerRegistryProtectionTagRule</c>,
///     introduced GitLab 18.7) — which container image tag name patterns only roles at or above the stated
///     levels may push or delete. Distinct from <see cref="ContainerRegistryProtectionRuleSummary" />, which
///     protects repository *path* patterns rather than tag patterns within a repository.
/// </summary>
public sealed record ContainerRegistryProtectionTagRuleSummary(
    long Id,
    long? ProjectId,
    string? TagNamePattern,
    string? MinimumAccessLevelForPush,
    string? MinimumAccessLevelForDelete);

public sealed record ContainerRegistryProtectionTagRuleListResult(
    IReadOnlyList<ContainerRegistryProtectionTagRuleSummary> Rules,
    bool Truncated);

/// <summary>All-scalar confirmation of a delete — no GitLab-authored string, so the tool returns this bare (unwrapped).</summary>
public sealed record ContainerRegistryProtectionTagRuleDeleteResult(long RuleId, bool Deleted);