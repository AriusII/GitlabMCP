using GitLab.Client.Models;
using GitlabMCP.Contracts.MergeRequests;

namespace GitlabMCP.Mapping.MergeRequests;

public static class MergeTrainMapper
{
    public static MergeTrainCarSummary ToSummary(GitLabMergeTrainCar car)
    {
        return new MergeTrainCarSummary(
            car.Id,
            car.MergeRequest?.Iid,
            car.MergeRequest?.Title,
            car.TargetBranch,
            car.Status,
            car.User?.Username,
            car.CreatedAt,
            car.MergedAt);
    }
}