namespace GitlabMCP.Contracts.Code;

/// <summary>
///     Repository/commit projection records (domain "code"). Every string here is GitLab-authored —
///     commit messages, author names, branch/tag names, status descriptions — so every tool returning one of
///     these wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record CommitCommentSummary(
    string? Note,
    string? Path,
    int? Line,
    string? LineType,
    string? AuthorUsername,
    DateTimeOffset? CreatedAt);

public sealed record CommitCommentListResult(IReadOnlyList<CommitCommentSummary> Comments, bool Truncated);

/// <summary>
///     Per-author commit metrics. <c>Email</c> is a git commit identity (as it appears in commit history),
///     not <c>GitLabUser.Email</c> — the same category as <see cref="CommitSummary.AuthorEmail" />, already
///     public within the repository's own commit log, not the banned account-PII field.
/// </summary>
public sealed record ContributorSummary(
    string? Name,
    string? Email,
    int? Commits,
    int? Additions,
    int? Deletions);

public sealed record ContributorListResult(IReadOnlyList<ContributorSummary> Contributors, bool Truncated);

public sealed record CommitStatusSummary(
    long Id,
    string? Sha,
    string? Ref,
    string? Status,
    string? Name,
    string? TargetUrl,
    string? Description,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? FinishedAt,
    bool? AllowFailure,
    string? AuthorUsername);

public sealed record CommitStatusListResult(IReadOnlyList<CommitStatusSummary> Statuses, bool Truncated);

/// <summary>
///     Commit metadata as returned by <c>gitlab_list_commits</c>, <c>gitlab_create_commit</c>,
///     <c>gitlab_cherry_pick_commit</c>, <c>gitlab_revert_commit</c>, and the commit/diff lists inside
///     <c>gitlab_compare_refs</c>. Trailers/stats/pipeline are omitted — see
///     <see cref="CommitDetailSummary" /> when line-change stats are actually wanted.
/// </summary>
public sealed record CommitSummary(
    string? Id,
    string? ShortId,
    string? Title,
    string? Message,
    string? AuthorName,
    string? AuthorEmail,
    DateTimeOffset? AuthoredDate,
    string? WebUrl,
    IReadOnlyList<string> ParentIds);

public sealed record CommitListResult(IReadOnlyList<CommitSummary> Commits, bool Truncated);

/// <summary>Full detail for <c>gitlab_get_commit</c>, including the line-change stats <see cref="CommitSummary" /> omits.</summary>
public sealed record CommitDetailSummary(
    string? Id,
    string? ShortId,
    string? Title,
    string? Message,
    string? AuthorName,
    string? AuthorEmail,
    DateTimeOffset? AuthoredDate,
    string? CommitterName,
    string? CommitterEmail,
    DateTimeOffset? CommittedDate,
    string? WebUrl,
    IReadOnlyList<string> ParentIds,
    int? Additions,
    int? Deletions,
    int? Total,
    string? Status);

/// <summary>
///     One file's diff, from <c>gitlab_get_commit_diff</c> or <c>gitlab_compare_refs</c>.
///     <see cref="DiffSummary.Diff" /> is truncated independently of the list-level limit — a single
///     file's unified diff can fill a context window on its own.
/// </summary>
public sealed record DiffSummary(
    string? OldPath,
    string? NewPath,
    bool? NewFile,
    bool? RenamedFile,
    bool? DeletedFile,
    string? Diff,
    bool DiffTruncated);

public sealed record DiffListResult(IReadOnlyList<DiffSummary> Diffs, bool Truncated);

/// <summary>One merge request a commit belongs to, from <c>gitlab_list_commit_merge_requests</c>.</summary>
public sealed record CommitMergeRequestSummary(
    long Iid,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? WebUrl);

public sealed record CommitMergeRequestListResult(
    IReadOnlyList<CommitMergeRequestSummary> MergeRequests,
    bool Truncated);

/// <summary>Result of <c>gitlab_compare_refs</c>: the commits and diffs between two refs, each independently bounded.</summary>
public sealed record CompareResult(
    IReadOnlyList<CommitSummary> Commits,
    bool CommitsTruncated,
    IReadOnlyList<DiffSummary> Diffs,
    bool DiffsTruncated,
    bool? CompareTimeout,
    bool? CompareSameRef,
    string? WebUrl);