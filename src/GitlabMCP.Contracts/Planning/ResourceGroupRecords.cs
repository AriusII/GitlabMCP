namespace GitlabMCP.Contracts.Planning;

/// <summary>
///     A CI/CD resource group (deployment-concurrency mutex). <see cref="Key" /> is whatever name
///     the project's <c>.gitlab-ci.yml</c> gave the <c>resource_group</c> keyword — GitLab-authored text.
/// </summary>
public sealed record ResourceGroupSummary(
    long Id,
    string? Key,
    string? ProcessMode,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ResourceGroupListResult(IReadOnlyList<ResourceGroupSummary> ResourceGroups, bool Truncated);

/// <summary>
///     Minimal projection of <c>GitLab.Client.Models.GitLabJob</c> for resource-group occupancy —
///     not the full CI/CD job surface (the cicd domain owns that one separately).
/// </summary>
public sealed record JobSummary(
    long Id,
    string? Status,
    string? Stage,
    string? Name,
    string? Ref,
    DateTimeOffset? CreatedAt,
    string? WebUrl);

/// <summary>
///     A resource group plus its live occupancy: which job currently holds it (null when idle —
///     nothing holds a resource group most of the time) and which jobs are queued behind it, in the order
///     the process mode will release them.
/// </summary>
public sealed record ResourceGroupDetail(
    long Id,
    string? Key,
    string? ProcessMode,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    JobSummary? CurrentJob,
    IReadOnlyList<JobSummary> UpcomingJobs,
    bool UpcomingJobsTruncated);