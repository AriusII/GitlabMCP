namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     A merge request associated with a deployment, projected independently of the
///     merge-requests domain's own records to keep this domain self-contained. Title/branch names are
///     GitLab-authored text, so the tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DeploymentMergeRequestSummary(
    long Iid,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? WebUrl);

public sealed record DeploymentMergeRequestListResult(
    IReadOnlyList<DeploymentMergeRequestSummary> MergeRequests,
    bool Truncated);

/// <summary>
///     Deployment projection. Ref/Sha/Status and the associated environment/user names all
///     originate on GitLab, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DeploymentSummary(
    long Id,
    long? Iid,
    string? Ref,
    string? Sha,
    string? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? UserUsername,
    long? EnvironmentId,
    string? EnvironmentName);

public sealed record DeploymentListResult(IReadOnlyList<DeploymentSummary> Deployments, bool Truncated);

/// <summary>
///     Full deployment detail, including the triggering CI job and the most recent approval
///     decision. GitLab-authored text throughout, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record DeploymentDetail(
    long Id,
    long? Iid,
    string? Ref,
    string? Sha,
    string? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? UserUsername,
    long? EnvironmentId,
    string? EnvironmentName,
    long? DeployableId,
    string? DeployableName,
    string? DeployableStatus,
    string? DeployableWebUrl,
    int? PendingApprovalCount,
    string? LatestApprovalStatus,
    string? LatestApprovalUserUsername,
    DateTimeOffset? LatestApprovalCreatedAt);

public sealed record DeploymentDeleteResult(long DeploymentId, bool Deleted);

/// <summary>
///     Confirms an approval or rejection was recorded against a deployment. Status/comment/username
///     are GitLab-authored or caller-supplied text round-tripped through GitLab, so the tool returning this
///     wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DeploymentApprovalSummary(
    string? UserUsername,
    string? Status,
    DateTimeOffset? CreatedAt,
    string? Comment);