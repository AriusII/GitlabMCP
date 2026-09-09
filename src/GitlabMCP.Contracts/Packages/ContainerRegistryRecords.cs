namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     Container image repository projection (<c>GitLabRegistryRepository</c>). <see cref="Location" /> is the
///     registry pull address and <see cref="Path" /> the repository path — both GitLab-authored (they embed the
///     project/group path) — so every tool returning this wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record ContainerRepositorySummary(
    long Id,
    string? Name,
    string? Path,
    long ProjectId,
    string? Location,
    DateTimeOffset CreatedAt,
    int? TagsCount,
    long? Size,
    string? Status);

public sealed record ContainerRepositoryListResult(
    IReadOnlyList<ContainerRepositorySummary> Repositories,
    bool Truncated);

/// <summary>All-scalar confirmation of a delete — no GitLab-authored string, so the tool returns this bare (unwrapped).</summary>
public sealed record ContainerRepositoryDeleteResult(long RepositoryId, bool Deleted);

/// <summary>
///     One tag on a container repository (<c>GitLabRegistryRepositoryTag</c>) — <see cref="Location" /> embeds
///     the repository path, so every tool returning this wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record ContainerRepositoryTagSummary(string? Name, string? Path, string? Location);

public sealed record ContainerRepositoryTagListResult(
    IReadOnlyList<ContainerRepositoryTagSummary> Tags,
    bool Truncated);

/// <summary>Full detail for one image tag (<c>GitLabRegistryRepositoryTagDetails</c>) — digest, size, and revision.</summary>
public sealed record ContainerRepositoryTagDetail(
    string? Name,
    string? Path,
    string? Location,
    string? Revision,
    string? ShortRevision,
    string? Digest,
    DateTimeOffset? CreatedAt,
    long? TotalSize);

/// <summary>
///     All-scalar confirmation of a tag delete — no GitLab-authored string, so the tool returns this bare
///     (unwrapped).
/// </summary>
public sealed record ContainerRepositoryTagDeleteResult(long RepositoryId, bool Deleted);

/// <summary>
///     All-scalar acknowledgement of a scheduled bulk tag delete — GitLab performs the removal
///     asynchronously, so this confirms only that it was queued. No GitLab-authored string, so the tool
///     returns this bare (unwrapped).
/// </summary>
public sealed record ContainerRepositoryTagsBulkDeleteResult(long RepositoryId, bool Scheduled);