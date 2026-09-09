namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabProject</c> as returned by <c>ISearchClient</c>'s project
///     search. Name/PathWithNamespace/Description are GitLab-authored, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchProjectSummary(
    long Id,
    string? Name,
    string? PathWithNamespace,
    string? Description,
    string? Visibility,
    string? WebUrl);

public sealed record SearchProjectListResult(IReadOnlyList<SearchProjectSummary> Projects, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabIssue</c> as returned by <c>ISearchClient</c>'s issue
///     search. Title/Labels/AuthorUsername are GitLab-authored, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchIssueSummary(
    long Iid,
    long? ProjectId,
    string? Title,
    string? State,
    string? AuthorUsername,
    IReadOnlyList<string> Labels,
    string? WebUrl);

public sealed record SearchIssueListResult(IReadOnlyList<SearchIssueSummary> Issues, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabMergeRequest</c> as returned by <c>ISearchClient</c>'s
///     merge request search. Title/SourceBranch/TargetBranch/AuthorUsername are GitLab-authored, so every
///     tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchMergeRequestSummary(
    long Iid,
    long? ProjectId,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? AuthorUsername,
    string? WebUrl);

public sealed record SearchMergeRequestListResult(
    IReadOnlyList<SearchMergeRequestSummary> MergeRequests,
    bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabUser</c> as returned by <c>ISearchClient</c>'s user
///     search. Username/Name are GLOBAL, self-service GitLab-authored fields (mcp-untrusted-content's field
///     inventory — anyone can register and set them), so every tool returning this wraps via
///     <see cref="GitLabContent" />. Deliberately excludes Email/CommitEmail/Identities/IsAdmin/Note/
///     CustomAttributes and every other field on <c>mcp-untrusted-content</c> Step 3's banned list.
/// </summary>
public sealed record SearchUserSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl);

public sealed record SearchUserListResult(IReadOnlyList<SearchUserSummary> Users, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabMilestone</c> as returned by <c>ISearchClient</c>'s
///     milestone search. Title/Description are GitLab-authored, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchMilestoneSummary(
    long Id,
    long Iid,
    string? Title,
    string? Description,
    string? State,
    string? WebUrl);

public sealed record SearchMilestoneListResult(IReadOnlyList<SearchMilestoneSummary> Milestones, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabNote</c> as returned by <c>ISearchClient</c>'s note
///     search. Body/AuthorUsername are GitLab-authored, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchNoteSummary(
    long Id,
    string? Body,
    string? AuthorUsername,
    string? NoteableType,
    long? NoteableId,
    DateTimeOffset? CreatedAt);

public sealed record SearchNoteListResult(IReadOnlyList<SearchNoteSummary> Notes, bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabCommit</c> as returned by <c>ISearchClient</c>'s commit
///     search. Title/AuthorName are GitLab-authored commit metadata, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchCommitSummary(
    string? Id,
    string? ShortId,
    string? Title,
    string? AuthorName,
    DateTimeOffset? AuthoredDate,
    string? WebUrl);

public sealed record SearchCommitListResult(IReadOnlyList<SearchCommitSummary> Commits, bool Truncated);

/// <summary>One matched line range within a semantic code search hit.</summary>
public sealed record SemanticCodeSnippetRangeSummary(
    int? StartLine,
    int? EndLine,
    string? Content,
    double? Score);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabSemanticCodeSearchMatch</c>. <see cref="SnippetRanges" />
///     carries repository source code — GitLab-hosted, author-controlled text — so every tool returning this
///     wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record SemanticCodeSearchMatchSummary(
    string? Path,
    string? BlobId,
    string? FileUrl,
    double? Score,
    IReadOnlyList<SemanticCodeSnippetRangeSummary> SnippetRanges);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabSemanticCodeSearchResult</c> — one ranked result set, not
///     a paginated stream (<c>SearchProjectSemanticCodeAsync</c> returns <c>Task&lt;T&gt;</c>, not
///     <c>IAsyncEnumerable&lt;T&gt;</c> — one of gitlab-client-navigation's documented streaming false
///     positives), so <see cref="Results" /> is capped defensively rather than paged and <see cref="Truncated" />
///     reports whether the cap was hit.
/// </summary>
public sealed record SemanticCodeSearchSummary(
    string? Confidence,
    IReadOnlyList<SemanticCodeSearchMatchSummary> Results,
    bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabSearchMigration</c>. <c>MigrationState</c> is an untyped
///     <c>JsonElement</c> in the library (no declared schema for it) and is deliberately omitted rather than
///     passed through untyped — <c>gitlab_list_search_migrations</c> already exposes the raw JSON for callers
///     that need it. <see cref="Name" /> is GitLab-generated text, so the tool wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record SearchMigrationSummary(
    long Version,
    string? Name,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool? Completed,
    bool? Obsolete);