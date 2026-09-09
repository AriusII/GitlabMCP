namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     Pipeline provenance for a package version — which CI pipeline built it (<c>GitLabPipeline</c>,
///     narrowly projected for this domain; other domains project the same library type independently
///     rather than sharing a cross-domain record). Every string here is GitLab-authored, so the tool
///     returning it wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record PackagePipelineSummary(
    long Id,
    long? Iid,
    string? Status,
    string? Ref,
    string? Sha,
    DateTimeOffset? CreatedAt,
    string? WebUrl);

public sealed record PackagePipelineListResult(IReadOnlyList<PackagePipelineSummary> Pipelines, bool Truncated);