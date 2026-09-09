namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     Cross-format package projection (<c>GitLabPackage</c>) — the "what have we published" summary view
///     shared by <c>gitlab_list_packages</c>-shaped tools at both project and group scope. Every string here
///     (name, version, project path) is GitLab-authored — every tool returning it wraps via
///     <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record PackageSummary(
    long Id,
    string? Name,
    string? Version,
    string? PackageType,
    string? Status,
    DateTimeOffset? CreatedAt,
    long? ProjectId,
    string? ProjectPath);

public sealed record PackageListResult(IReadOnlyList<PackageSummary> Packages, bool Truncated);

/// <summary>All-scalar confirmation of a delete — no GitLab-authored string, so the tool returns this bare (unwrapped).</summary>
public sealed record PackageDeleteResult(long PackageId, bool Deleted);

/// <summary>
///     Fuller single-package projection (<c>GitLabPackage</c>) returned by <c>gitlab_get_package</c> —
///     adds creator/download/pipeline-provenance fields on top of <see cref="PackageSummary" />. Every
///     string here is GitLab-authored, so the tool returning this wraps via
///     <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record PackageDetail(
    long Id,
    string? Name,
    string? Version,
    string? PackageType,
    string? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastDownloadedAt,
    long? CreatorId,
    long? ProjectId,
    string? ProjectPath,
    string? Tags,
    long? PipelineId,
    string? PipelineStatus,
    string? PipelineRef);