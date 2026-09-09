using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

/// <summary>
///     Projects <see cref="GitLabIssue" /> into the owned Planning-domain records — never hands back
///     the library DTO (45 direct properties, nine of which expand into further model types).
/// </summary>
public static class IssueMapper
{
    public static IssueSummary ToSummary(GitLabIssue issue)
    {
        return new IssueSummary(
            issue.Iid,
            issue.Title,
            issue.State,
            issue.Author?.Username,
            issue.Labels ?? [],
            issue.Milestone?.Title,
            issue.Weight,
            issue.Severity,
            issue.Confidential,
            issue.UpdatedAt,
            issue.WebUrl?.ToString());
    }

    public static IssueLinkSummary ToLinkSummary(GitLabIssue issue)
    {
        return new IssueLinkSummary(
            issue.Iid,
            issue.Title,
            issue.State,
            issue.IssueLinkId,
            issue.LinkType,
            issue.WebUrl?.ToString());
    }

    public static IssueSubscriptionResult ToSubscriptionResult(GitLabIssue issue)
    {
        return new IssueSubscriptionResult(
            issue.Iid,
            issue.Title,
            issue.Subscribed,
            issue.WebUrl?.ToString());
    }

    public static ParticipantSummary ToParticipantSummary(GitLabUser user)
    {
        return new ParticipantSummary(
            user.Id,
            user.Username,
            user.Name,
            user.WebUrl?.ToString());
    }

    public static TimeStatsSummary ToTimeStats(GitLabTimeStats stats)
    {
        return new TimeStatsSummary(
            stats.TimeEstimate,
            stats.TotalTimeSpent,
            stats.HumanTimeEstimate,
            stats.HumanTotalTimeSpent);
    }

    public static IssueStatisticsResult ToStatisticsResult(GitLabIssueStatistics statistics)
    {
        var counts = statistics.Statistics?.Counts;
        return new IssueStatisticsResult(counts?.Opened, counts?.Closed, counts?.All);
    }

    public static IssueLinkResult ToLinkResult(GitLabIssueLink link)
    {
        return new IssueLinkResult(
            link.Id,
            link.LinkType,
            link.SourceIssue is null ? null : ToSummary(link.SourceIssue),
            link.TargetIssue is null ? null : ToSummary(link.TargetIssue));
    }

    public static RelatedMergeRequestSummary ToRelatedMergeRequestSummary(GitLabMergeRequest mergeRequest)
    {
        return new RelatedMergeRequestSummary(
            mergeRequest.Iid,
            mergeRequest.Title,
            mergeRequest.State,
            mergeRequest.SourceBranch,
            mergeRequest.TargetBranch,
            mergeRequest.Author?.Username,
            mergeRequest.WebUrl?.ToString());
    }
}