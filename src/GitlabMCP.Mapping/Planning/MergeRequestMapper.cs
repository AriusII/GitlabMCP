using GitLab.Client.Models;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Mapping.Planning;

/// <summary>
///     Merge-request projections owned by the Planning domain — just the subscription result. The
///     full merge-request lifecycle lives in the mergerequests domain's own mapper.
/// </summary>
public static class MergeRequestMapper
{
    public static MergeRequestSubscriptionResult ToSubscriptionResult(GitLabMergeRequest mergeRequest)
    {
        return new MergeRequestSubscriptionResult(
            mergeRequest.Iid,
            mergeRequest.Title,
            mergeRequest.Subscribed,
            mergeRequest.WebUrl?.ToString());
    }
}