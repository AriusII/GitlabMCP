namespace GitlabMCP.Contracts.MergeRequests;

/// <summary>
///     Compact per-merge-request projection for list results. Every string here is GitLab-authored — tools returning
///     it wrap via <see cref="GitLabContent" />.
/// </summary>
public sealed record MergeRequestSummary(
    long Iid,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? Author,
    IReadOnlyList<string> Labels,
    bool? Draft,
    string? MergeStatus,
    DateTimeOffset? UpdatedAt,
    string? WebUrl);

public sealed record MergeRequestListResult(IReadOnlyList<MergeRequestSummary> MergeRequests, bool Truncated);

/// <summary>
///     Fuller projection returned by create/update, which includes the description the caller just set.
///     <see cref="Description" /> is capped (see <c>MergeRequestMapper.MaxDescriptionPreviewChars</c>) --
///     GitLab imposes no meaningfully small bound on a merge request description's length (up to
///     ~1,048,576 characters), so a single large description could otherwise fill the caller's context
///     budget on its own; <see cref="DescriptionTruncated" /> reports whether it was cut.
/// </summary>
public sealed record MergeRequestResult(
    long Iid,
    string? Title,
    string? Description,
    bool DescriptionTruncated,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    IReadOnlyList<string> Labels,
    bool? Draft,
    string? MergeStatus,
    string? WebUrl);

public sealed record CommitSummary(
    string Sha,
    string? ShortId,
    string? Title,
    string? AuthorName,
    DateTimeOffset? AuthoredDate,
    string? WebUrl);

public sealed record MergeRequestCommitListResult(IReadOnlyList<CommitSummary> Commits, bool Truncated);

public sealed record PipelineSummary(
    long Id,
    long? Iid,
    string? Status,
    string? Ref,
    string? Sha,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? WebUrl);

public sealed record MergeRequestPipelineListResult(IReadOnlyList<PipelineSummary> Pipelines, bool Truncated);

public sealed record IssueRefSummary(long Iid, string? Title, string? State, string? WebUrl);

public sealed record MergeRequestIssueListResult(IReadOnlyList<IssueRefSummary> Issues, bool Truncated);

public sealed record ReviewerSummary(string? Username, string? Name, string? State);

public sealed record MergeRequestReviewerListResult(IReadOnlyList<ReviewerSummary> Reviewers, bool Truncated);

/// <summary>
///     Result of <c>gitlab_delete_merge_request</c>. No GitLab-authored string in the payload -- returned bare, not
///     wrapped.
/// </summary>
public sealed record MergeRequestDeleteResult(bool Deleted);

/// <summary>
///     Result of queuing a rebase. <c>MergeError</c> is GitLab-authored free text when the rebase failed -- tools
///     returning this wrap via <see cref="GitLabContent" />.
/// </summary>
public sealed record MergeRequestRebaseResult(bool? RebaseInProgress, string? MergeError);

/// <summary>
///     One file's diff within a merge request. <c>Diff</c> is capped per-file so a handful of large files cannot fill
///     the model's context window on their own -- <see cref="DiffTruncated" /> reports that case. Separately, GitLab
///     itself can suppress a file's diff body server-side for being oversized (its <c>too_large</c>/<c>collapsed</c>
///     flags); <see cref="DiffSuppressed" /> reports that case so it is never confused with a file that genuinely has
///     no changes.
/// </summary>
public sealed record DiffFileSummary(
    string? OldPath,
    string? NewPath,
    bool? NewFile,
    bool? RenamedFile,
    bool? DeletedFile,
    string? Diff,
    bool DiffTruncated,
    bool DiffSuppressed);

public sealed record MergeRequestDiffListResult(IReadOnlyList<DiffFileSummary> Files, bool Truncated);