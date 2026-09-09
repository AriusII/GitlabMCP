using GitLab.Client.Models;
using GitlabMCP.Contracts.Code;

namespace GitlabMCP.Mapping.Code;

/// <summary>Projects commit/comment/contributor/status DTOs into the owned "code" records.</summary>
public static class CommitMapper
{
    public static CommitCommentSummary ToSummary(GitLabCommitComment comment)
    {
        return new CommitCommentSummary(
            comment.Note,
            comment.Path,
            comment.Line,
            comment.LineType,
            comment.Author?.Username,
            comment.CreatedAt);
    }

    public static ContributorSummary ToSummary(GitLabContributor contributor)
    {
        return new ContributorSummary(
            contributor.Name,
            contributor.Email,
            contributor.Commits,
            contributor.Additions,
            contributor.Deletions);
    }

    public static CommitStatusSummary ToSummary(GitLabCommitStatus status)
    {
        return new CommitStatusSummary(
            status.Id,
            status.Sha,
            status.Ref,
            status.Status,
            status.Name,
            status.TargetUrl?.ToString(),
            status.Description,
            status.CreatedAt,
            status.FinishedAt,
            status.AllowFailure,
            status.Author?.Username);
    }

    public static CommitSummary ToSummary(GitLabCommit commit)
    {
        return new CommitSummary(
            commit.Id,
            commit.ShortId,
            commit.Title,
            commit.Message,
            commit.AuthorName,
            commit.AuthorEmail,
            commit.AuthoredDate,
            commit.WebUrl?.ToString(),
            commit.ParentIds ?? []);
    }

    public static CommitDetailSummary ToDetailSummary(GitLabCommit commit)
    {
        return new CommitDetailSummary(
            commit.Id,
            commit.ShortId,
            commit.Title,
            commit.Message,
            commit.AuthorName,
            commit.AuthorEmail,
            commit.AuthoredDate,
            commit.CommitterName,
            commit.CommitterEmail,
            commit.CommittedDate,
            commit.WebUrl?.ToString(),
            commit.ParentIds ?? [],
            commit.Stats?.Additions,
            commit.Stats?.Deletions,
            commit.Stats?.Total,
            commit.Status);
    }

    /// <summary>
    ///     A single file's diff. <see cref="DiffSummary.Diff" /> is truncated here, independently of the caller's
    ///     list-level limit.
    /// </summary>
    public static DiffSummary ToSummary(GitLabDiff diff)
    {
        const int MaxDiffChars = 4000;

        var text = diff.Diff;
        var truncated = false;

        if (text is not { Length: > MaxDiffChars })
            return new DiffSummary(
                diff.OldPath,
                diff.NewPath,
                diff.NewFile,
                diff.RenamedFile,
                diff.DeletedFile,
                text,
                truncated);
        text = text[..MaxDiffChars];
        truncated = true;

        return new DiffSummary(
            diff.OldPath,
            diff.NewPath,
            diff.NewFile,
            diff.RenamedFile,
            diff.DeletedFile,
            text,
            truncated);
    }

    public static CommitMergeRequestSummary ToSummary(GitLabMergeRequest mergeRequest)
    {
        return new CommitMergeRequestSummary(
            mergeRequest.Iid,
            mergeRequest.Title,
            mergeRequest.State,
            mergeRequest.SourceBranch,
            mergeRequest.TargetBranch,
            mergeRequest.WebUrl?.ToString());
    }
}