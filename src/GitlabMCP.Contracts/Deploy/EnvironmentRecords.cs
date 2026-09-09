namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Deployment environment projection. Name/description are caller-authored and round-tripped
///     through GitLab, so the tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record EnvironmentSummary(
    long Id,
    string? Name,
    string? Slug,
    string? ExternalUrl,
    string? State,
    string? Tier,
    string? Description);

public sealed record EnvironmentListResult(IReadOnlyList<EnvironmentSummary> Environments, bool Truncated);

/// <summary>
///     Every field is a non-string scalar, so the tool returning this declares the bare record
///     rather than <see cref="GitLabContent.Wrap{T}" />.
/// </summary>
public sealed record EnvironmentDeleteResult(long EnvironmentId, bool Deleted);

/// <summary>
///     Every field is a non-string scalar, so the tool returning this declares the bare record
///     rather than <see cref="GitLabContent.Wrap{T}" />.
/// </summary>
public sealed record StopStaleEnvironmentsResult(DateTimeOffset Before, bool Stopped);

/// <summary>
///     Confirms a review-app deletion request was submitted. Every field is a non-string scalar, so the
///     tool returning this declares the bare record rather than <see cref="GitLabContent.Wrap{T}" />. GitLab
///     computes a scheduled/preview list for this call but <c>IEnvironmentsClient.DeleteReviewAppsAsync</c>
///     deliberately discards it rather than surfacing it as a typed result - see that method's own remarks
///     - so there is nothing else to report here.
/// </summary>
public sealed record ReviewAppDeletionResult(bool DryRun, DateTimeOffset? Before, int? Limit, bool Requested);