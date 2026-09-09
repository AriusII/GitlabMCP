using GitLab.Client.Models;
using GitlabMCP.Contracts.MergeRequests;

namespace GitlabMCP.Mapping.MergeRequests;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> merge-request-adjacent DTOs into the owned records in
///     <see cref="GitlabMCP.Contracts.MergeRequests" /> — never hands back the library's DTO.
/// </summary>
public static class MergeRequestMapper
{
    /// <summary>
    ///     Per-file cap on <see cref="DiffFileSummary.Diff" />. A single file's diff is author-controlled and unbounded,
    ///     and a merge request can carry many files, so each is capped independently.
    /// </summary>
    private const int MaxDiffPreviewChars = 4_000;

    /// <summary>
    ///     Cap on <see cref="MergeRequestResult.Description" />. GitLab imposes no meaningfully small bound on a
    ///     merge request description's length (up to ~1,048,576 characters), so -- unlike the per-file diff cap above,
    ///     which bounds many files at once -- a single such field could still fill the caller's context budget on
    ///     its own if left unbounded.
    /// </summary>
    private const int MaxDescriptionPreviewChars = 8_000;

    public static MergeRequestSummary ToSummary(GitLabMergeRequest mr)
    {
        return new MergeRequestSummary(
            mr.Iid,
            mr.Title,
            mr.State,
            mr.SourceBranch,
            mr.TargetBranch,
            mr.Author?.Username,
            mr.Labels ?? [],
            mr.Draft,
            mr.MergeStatus,
            mr.UpdatedAt,
            mr.WebUrl?.ToString());
    }

    public static MergeRequestResult ToResult(GitLabMergeRequest mr)
    {
        var (description, descriptionTruncated) = Truncate(mr.Description, MaxDescriptionPreviewChars);
        return new MergeRequestResult(
            mr.Iid,
            mr.Title,
            description,
            descriptionTruncated,
            mr.State,
            mr.SourceBranch,
            mr.TargetBranch,
            mr.Labels ?? [],
            mr.Draft,
            mr.MergeStatus,
            mr.WebUrl?.ToString());
    }

    public static CommitSummary ToCommitSummary(GitLabCommit commit)
    {
        return new CommitSummary(
            commit.Id,
            commit.ShortId,
            commit.Title,
            commit.AuthorName,
            commit.AuthoredDate,
            commit.WebUrl?.ToString());
    }

    public static PipelineSummary ToPipelineSummary(GitLabPipeline pipeline)
    {
        return new PipelineSummary(
            pipeline.Id,
            pipeline.Iid,
            pipeline.Status,
            pipeline.Ref,
            pipeline.Sha,
            pipeline.CreatedAt,
            pipeline.UpdatedAt,
            pipeline.WebUrl?.ToString());
    }

    public static IssueRefSummary ToIssueRefSummary(GitLabIssue issue)
    {
        return new IssueRefSummary(
            issue.Iid,
            issue.Title,
            issue.State,
            issue.WebUrl?.ToString());
    }

    public static ReviewerSummary ToReviewerSummary(GitLabMergeRequestReviewer reviewer)
    {
        return new ReviewerSummary(
            reviewer.User?.Username,
            reviewer.User?.Name,
            reviewer.State);
    }

    public static MergeRequestRebaseResult ToRebaseResult(GitLabMergeRequestRebaseResult result)
    {
        return new MergeRequestRebaseResult(
            result.RebaseInProgress,
            result.MergeError);
    }

    public static DiffFileSummary ToDiffFileSummary(GitLabDiff diff)
    {
        var (text, truncated) = TruncateDiff(diff.Diff);

        // GitLab suppresses a file's diff body server-side (too_large/collapsed) independently of whether
        // our own per-file cap above ever gets a chance to trigger -- the field arrives null/empty either
        // way. Without this, a suppressed file is indistinguishable from one with genuinely no changes.
        var suppressed = diff.TooLarge == true || diff.Collapsed == true;

        return new DiffFileSummary(
            diff.OldPath,
            diff.NewPath,
            diff.NewFile,
            diff.RenamedFile,
            diff.DeletedFile,
            text,
            truncated,
            suppressed);
    }

    private static (string? Diff, bool Truncated) TruncateDiff(string? diff)
    {
        return Truncate(diff, MaxDiffPreviewChars);
    }

    private static (string? Text, bool Truncated) Truncate(string? text, int maxChars)
    {
        if (text is null) return (null, false);

        return text.Length > maxChars
            ? (text[..maxChars], true)
            : (text, false);
    }
}