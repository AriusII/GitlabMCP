using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

public static class BoardMapper
{
    public static BoardSummary ToSummary(GitLabBoard board)
    {
        return new BoardSummary(
            board.Id,
            board.Name,
            board.Weight,
            board.HideBacklogList,
            board.HideClosedList,
            board.Lists?.Count ?? 0);
    }

    public static BoardColumnSummary ToColumnSummary(GitLabBoardList list)
    {
        return new BoardColumnSummary(
            list.Id,
            list.Position,
            list.Label?.Name,
            list.Milestone?.Title,
            list.Iteration?.Title,
            list.LimitMetric,
            list.MaxIssueCount,
            list.MaxIssueWeight);
    }

    /// <summary>
    ///     Full projection including <see cref="GitLabBoard.Lists" />, unlike <see cref="ToSummary" />
    ///     which only counts them.
    /// </summary>
    public static BoardDetail ToDetail(GitLabBoard board)
    {
        return new BoardDetail(
            board.Id,
            board.Name,
            board.Weight,
            board.HideBacklogList,
            board.HideClosedList,
            board.Lists?.Select(ToColumnSummary).ToList() ?? []);
    }
}