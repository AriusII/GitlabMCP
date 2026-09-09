namespace GitlabMCP.Contracts.Planning;

public sealed record MilestoneSummary(
    long Id,
    long Iid,
    string? Title,
    string? Description,
    string? State,
    DateOnly? StartDate,
    DateOnly? DueDate,
    string? WebUrl);

public sealed record MilestoneListResult(IReadOnlyList<MilestoneSummary> Milestones, bool Truncated);

/// <summary>
///     One burndown event of a milestone (Premium/Ultimate only). <c>Action</c> is GitLab's own
///     fixed vocabulary ("created"/"closed"/"reopened" etc.), not free text, but it still travels through
///     <c>GitLabContent</c> along with the rest of the result.
/// </summary>
public sealed record BurndownEventSummary(string? Action, DateTimeOffset? CreatedAt, int? Weight);

public sealed record BurndownEventListResult(IReadOnlyList<BurndownEventSummary> Events, bool Truncated);

/// <summary>
///     Bare record: both fields are server/caller-supplied scalars, no GitLab-authored string
///     involved (<c>IMilestonesClient.DeleteAsync</c>/<c>DeleteForGroupAsync</c> return no body).
/// </summary>
public sealed record MilestoneDeleteResult(long MilestoneId, bool Deleted);

/// <summary>
///     Bare record, same reasoning as <see cref="MilestoneDeleteResult" />
///     (<c>IMilestonesClient.PromoteAsync</c> returns no body). Project-scoped only — there is no
///     group-to-higher-scope promotion.
/// </summary>
public sealed record MilestonePromoteResult(long MilestoneId, bool Promoted);

/// <summary>
///     Reuses <see cref="RelatedMergeRequestSummary" /> (declared alongside the issue-adjacent
///     merge-request projection in IssueRecords.cs) for the merge requests assigned to a milestone.
/// </summary>
public sealed record MilestoneMergeRequestListResult(
    IReadOnlyList<RelatedMergeRequestSummary> MergeRequests,
    bool Truncated);