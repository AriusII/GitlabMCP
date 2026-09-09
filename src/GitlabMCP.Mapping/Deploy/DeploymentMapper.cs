using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class DeploymentMapper
{
    public static DeploymentMergeRequestSummary ToSummary(GitLabMergeRequest mergeRequest)
    {
        return new DeploymentMergeRequestSummary(
            mergeRequest.Iid,
            mergeRequest.Title,
            mergeRequest.State,
            mergeRequest.SourceBranch,
            mergeRequest.TargetBranch,
            mergeRequest.WebUrl.ToString());
    }

    public static DeploymentSummary ToSummary(GitLabDeployment deployment)
    {
        return new DeploymentSummary(
            deployment.Id,
            deployment.Iid,
            deployment.Ref,
            deployment.Sha,
            deployment.Status,
            deployment.CreatedAt,
            deployment.UpdatedAt,
            deployment.User?.Username,
            deployment.Environment?.Id,
            deployment.Environment?.Name);
    }

    public static DeploymentDetail ToDetail(GitLabDeployment deployment)
    {
        return new DeploymentDetail(
            deployment.Id,
            deployment.Iid,
            deployment.Ref,
            deployment.Sha,
            deployment.Status,
            deployment.CreatedAt,
            deployment.UpdatedAt,
            deployment.User?.Username,
            deployment.Environment?.Id,
            deployment.Environment?.Name,
            deployment.Deployable?.Id,
            deployment.Deployable?.Name,
            deployment.Deployable?.Status,
            deployment.Deployable?.WebUrl?.ToString(),
            deployment.PendingApprovalCount,
            deployment.Approvals?.Status,
            deployment.Approvals?.User?.Username,
            deployment.Approvals?.CreatedAt);
    }

    public static DeploymentApprovalSummary ToApprovalSummary(GitLabDeploymentApproval approval)
    {
        return new DeploymentApprovalSummary(
            approval.User?.Username,
            approval.Status,
            approval.CreatedAt,
            approval.Comment);
    }
}