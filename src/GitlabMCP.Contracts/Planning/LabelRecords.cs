namespace GitlabMCP.Contracts.Planning;

/// <summary>
///     Shared shape for both <c>GitLabLabel</c> (project-scoped) and <c>GitLabGroupLabel</c>
///     (group-scoped) — the two DTOs differ only in that a project label carries <see cref="Priority" /> and
///     an <c>IsProjectLabel</c> flag this record doesn't need. <see cref="Priority" /> is always null for a
///     group label.
/// </summary>
public sealed record LabelSummary(
    long Id,
    string? Name,
    string? Color,
    string? TextColor,
    string? Description,
    bool? Archived,
    int? Priority,
    int? OpenIssuesCount,
    int? ClosedIssuesCount,
    int? OpenMergeRequestsCount,
    bool? Subscribed);

public sealed record LabelListResult(IReadOnlyList<LabelSummary> Labels, bool Truncated);

/// <summary>
///     Bare record: <see cref="Name" /> is the caller-supplied identifier that named the label to
///     delete, not GitLab-authored text; <see cref="Deleted" /> is a server-computed scalar
///     (<c>ILabelsClient.DeleteAsync</c>/<c>DeleteForGroupAsync</c> return no body).
/// </summary>
public sealed record LabelDeleteResult(string Name, bool Deleted);

/// <summary>
///     Bare record, same reasoning as <see cref="LabelDeleteResult" />
///     (<c>ILabelsClient.PromoteAsync</c> returns no body). Project-scoped only — there is no
///     group-to-higher-scope promotion.
/// </summary>
public sealed record LabelPromoteResult(string Name, bool Promoted);