namespace GitlabMCP.Contracts.Packages;

/// <summary>One published version of a Terraform module, from the registry protocol's version list.</summary>
public sealed record TerraformModuleVersionSummary(string? Version, int SubmoduleCount);

/// <summary>
///     One module entry from <c>GitLabTerraformModuleVersionList.Modules</c> — the registry protocol wraps a
///     single module's version list in a one-element array by convention. <see cref="SourceUrl" /> is
///     GitLab/publisher-authored, so every tool returning this wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record TerraformModuleEntrySummary(
    string? SourceUrl,
    IReadOnlyList<TerraformModuleVersionSummary> Versions,
    bool VersionsTruncated);

public sealed record TerraformModuleVersionListResult(
    IReadOnlyList<TerraformModuleEntrySummary> Modules,
    bool Truncated);

/// <summary>
///     One Terraform module's registry metadata (<c>GitLabTerraformModule</c>), from the "get latest
///     published version" and "get specific version" Terraform Module Registry Protocol endpoints.
///     GitLab's response is a deliberately pruned subset of the community protocol — no readme, inputs,
///     outputs or resources. The root dependency graph on the underlying DTO is untyped JSON; it is
///     reported only as a count here rather than passed through raw. Every string here (name, provider,
///     version, source URL) is GitLab/publisher-authored, so every tool returning this wraps via
///     <see cref="GitlabMCP.Contracts.GitLabContent" />. <see cref="Versions" /> is
///     <c>GitLabTerraformModule.Versions</c>, documented as "every published version of this module", so
///     it is bounded by a caller-supplied limit the same way the sibling
///     <see cref="TerraformModuleEntrySummary.Versions" /> is; <see cref="VersionsTruncated" /> reports it.
/// </summary>
public sealed record TerraformModuleSummary(
    string? Name,
    string? Provider,
    IReadOnlyList<string> Providers,
    string? Version,
    IReadOnlyList<string> Versions,
    string? SourceUrl,
    int RootDependencyCount,
    IReadOnlyList<string> RootProviders,
    bool VersionsTruncated);