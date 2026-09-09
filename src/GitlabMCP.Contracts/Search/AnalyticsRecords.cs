namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabCodeReviewAnalyticsItem</c>. The library types
///     <c>Author</c>/<c>Milestone</c>/<c>ApprovedBy</c> as raw <c>JsonElement</c> (the spec declares no
///     schema for them there), so the mapper picks the one or two fields a caller needs out of each rather
///     than passing the element through. Title/AuthorUsername/MilestoneTitle/ApprovedByUsernames are all
///     GitLab-authored, so the tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record CodeReviewAnalyticsItemSummary(
    long Iid,
    string? Title,
    string? State,
    string? AuthorUsername,
    string? MilestoneTitle,
    IReadOnlyList<string> ApprovedByUsernames,
    int? NotesCount,
    int? ReviewTimeSeconds,
    string? DiffStats,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? WebUrl);

public sealed record CodeReviewAnalyticsListResult(IReadOnlyList<CodeReviewAnalyticsItemSummary> Items, bool Truncated);

/// <summary>
///     Combined counters behind <c>gitlab_get_group_activity_summary</c> — one call each to
///     <c>GetGroupActivityIssuesCountAsync</c>/<c>GetGroupActivityMergeRequestsCountAsync</c>/
///     <c>GetGroupActivityNewMembersCountAsync</c>, merged into one result. <see cref="GroupPath" /> is the
///     caller's own input, echoed back (not GitLab-authored); every other field is a plain count. No
///     GitLab-authored string reaches this record, so the tool returns it bare rather than through
///     <see cref="GitLabContent" /> (DEC-007's all-scalar exception).
/// </summary>
public sealed record GroupActivitySummaryResult(
    string GroupPath,
    int IssuesCount,
    int MergeRequestsCount,
    int NewMembersCount);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabDeploymentFrequency</c>. GitLab's OpenAPI document types
///     all three members as plain strings rather than numbers/dates (the library's own remark on the type),
///     so every field here is a GitLab-computed *string*, not a server-typed scalar — the tool wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record DeploymentFrequencyPointSummary(string? Value, string? From, string? To);

public sealed record DeploymentFrequencyListResult(
    IReadOnlyList<DeploymentFrequencyPointSummary> Points,
    bool Truncated);

/// <summary>
///     One point parsed out of the untyped JSON array GitLab's DORA metrics endpoints return (the library
///     types the whole response as <c>JsonElement</c> — no declared schema). <see cref="Date" /> is
///     GitLab-computed text, so the tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DoraMetricPointSummary(string? Date, double? Value);

/// <summary><see cref="Scope" /> and <see cref="Metric" /> are the caller's own validated input, echoed back for context.</summary>
public sealed record DoraMetricsResult(
    string Scope,
    string Metric,
    IReadOnlyList<DoraMetricPointSummary> Points,
    bool Truncated);