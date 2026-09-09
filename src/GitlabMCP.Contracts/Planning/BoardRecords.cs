namespace GitlabMCP.Contracts.Planning;

public sealed record BoardSummary(
    long Id,
    string? Name,
    int? Weight,
    bool? HideBacklogList,
    bool? HideClosedList,
    int ListCount);

public sealed record BoardSummaryListResult(IReadOnlyList<BoardSummary> Boards, bool Truncated);

/// <summary>
///     One column of an issue board — a plain list, a label list, a milestone list or an
///     iteration list, distinguished by which of <see cref="LabelName" />/<see cref="MilestoneTitle" />/
///     <see cref="IterationTitle" /> is non-null.
/// </summary>
public sealed record BoardColumnSummary(
    long Id,
    int? Position,
    string? LabelName,
    string? MilestoneTitle,
    string? IterationTitle,
    string? LimitMetric,
    int? MaxIssueCount,
    int? MaxIssueWeight);

public sealed record BoardColumnListResult(IReadOnlyList<BoardColumnSummary> Columns, bool Truncated);

/// <summary>
///     Full projection of one board, including its lists — unlike <see cref="BoardSummary" />, which
///     only carries a count. Returned by <c>gitlab_get_board</c>.
/// </summary>
public sealed record BoardDetail(
    long Id,
    string? Name,
    int? Weight,
    bool? HideBacklogList,
    bool? HideClosedList,
    IReadOnlyList<BoardColumnSummary> Lists);

/// <summary>
///     Bare record: both fields are server/caller-supplied scalars, no GitLab-authored string
///     involved (<c>IBoardsClient.DeleteForProjectAsync</c>/<c>DeleteForGroupAsync</c> return no body).
/// </summary>
public sealed record BoardDeleteResult(long BoardId, bool Deleted);

/// <summary>
///     Bare record, same reasoning as <see cref="BoardDeleteResult" />
///     (<c>IBoardsClient.DeleteListForProjectAsync</c>/<c>DeleteListForGroupAsync</c> return no body).
/// </summary>
public sealed record BoardListDeleteResult(long BoardId, long ListId, bool Deleted);