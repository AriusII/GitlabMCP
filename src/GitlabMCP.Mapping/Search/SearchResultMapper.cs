using GitLab.Client.Models;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Mapping.Search;

/// <summary>
///     Projects the various <c>GitLab.Client.Models</c> types <c>ISearchClient</c>'s search methods return
///     into the owned <c>Search*Summary</c> records — never hands back a library DTO. These all exist solely
///     to serve <c>ISearchClient</c>'s cross-cutting search surface (not one dedicated resource client each),
///     so they share this one file rather than being scattered across seven near-empty ones.
/// </summary>
public static class SearchResultMapper
{
    public static SearchProjectSummary ToSummary(GitLabProject project)
    {
        return new SearchProjectSummary(
            project.Id,
            project.Name,
            project.PathWithNamespace,
            project.Description,
            project.Visibility.ToString(),
            project.WebUrl?.ToString());
    }

    public static SearchIssueSummary ToSummary(GitLabIssue issue)
    {
        return new SearchIssueSummary(
            issue.Iid,
            issue.ProjectId,
            issue.Title,
            issue.State,
            issue.Author?.Username,
            issue.Labels ?? [],
            issue.WebUrl?.ToString());
    }

    public static SearchMergeRequestSummary ToSummary(GitLabMergeRequest mergeRequest)
    {
        return new SearchMergeRequestSummary(
            mergeRequest.Iid,
            mergeRequest.ProjectId,
            mergeRequest.Title,
            mergeRequest.State,
            mergeRequest.SourceBranch,
            mergeRequest.TargetBranch,
            mergeRequest.Author?.Username,
            mergeRequest.WebUrl?.ToString());
    }

    public static SearchUserSummary ToSummary(GitLabUser user)
    {
        return new SearchUserSummary(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.WebUrl?.ToString());
    }

    public static SearchMilestoneSummary ToSummary(GitLabMilestone milestone)
    {
        return new SearchMilestoneSummary(
            milestone.Id,
            milestone.Iid,
            milestone.Title,
            milestone.Description,
            milestone.State,
            milestone.WebUrl?.ToString());
    }

    public static SearchNoteSummary ToSummary(GitLabNote note)
    {
        return new SearchNoteSummary(
            note.Id,
            note.Body,
            note.Author?.Username,
            note.NoteableType,
            note.NoteableId,
            note.CreatedAt);
    }

    public static SearchCommitSummary ToSummary(GitLabCommit commit)
    {
        return new SearchCommitSummary(
            commit.Id,
            commit.ShortId,
            commit.Title,
            commit.AuthorName,
            commit.AuthoredDate,
            commit.WebUrl?.ToString());
    }

    public static SearchMigrationSummary ToSummary(GitLabSearchMigration migration)
    {
        return new SearchMigrationSummary(
            migration.Version,
            migration.Name,
            migration.StartedAt,
            migration.CompletedAt,
            migration.Completed,
            migration.Obsolete);
    }

    public static SemanticCodeSnippetRangeSummary ToSummary(GitLabSemanticCodeSnippetRange range)
    {
        return new SemanticCodeSnippetRangeSummary(
            range.StartLine,
            range.EndLine,
            range.Content,
            range.Score);
    }

    public static SemanticCodeSearchMatchSummary ToSummary(GitLabSemanticCodeSearchMatch match)
    {
        return new SemanticCodeSearchMatchSummary(
            match.Path,
            match.BlobId,
            match.FileUrl?.ToString(),
            match.Score,
            match.SnippetRanges?.Select(ToSummary).ToList() ?? []);
    }
}