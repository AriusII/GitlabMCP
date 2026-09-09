namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Runner projection. Description/tags/maintenance note are operator-authored free text stored
///     by GitLab, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerSummary(
    long Id,
    string? Description,
    string? RunnerType,
    string? Status,
    bool? Paused,
    bool? Online,
    bool? IsShared,
    bool? Locked,
    IReadOnlyList<string> TagList,
    string? AccessLevel,
    string? Version,
    string? IpAddress,
    DateTimeOffset? ContactedAt,
    string? MaintenanceNote);

public sealed record RunnerListResult(IReadOnlyList<RunnerSummary> Runners, bool Truncated);

/// <summary>
///     A runner controller's scope entry — <see cref="RunnerId" /> null means the instance-wide scope,
///     otherwise the id of one runner it governs. Every field is a non-string scalar (an id or a
///     timestamp), so the tools returning these declare the bare record directly rather than
///     <see cref="GitLabContent.Wrap{T}" /> — mcp-tool-authoring's one carve-out for an all-scalar payload.
/// </summary>
public sealed record RunnerControllerScopeSummary(long? RunnerId, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);

public sealed record RunnerControllerScopeListResult(
    IReadOnlyList<RunnerControllerScopeSummary> Scopes,
    bool Truncated);

/// <summary>
///     A controller token's metadata only — GitLab never returns the secret on creation; rotate it
///     separately to obtain one. <see cref="Description" /> is caller-supplied free text round-tripped
///     through GitLab, so the tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerControllerTokenSummary(
    long Id,
    long? RunnerControllerId,
    string? Description,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastUsedAt);

public sealed record RunnerControllerTokenListResult(
    IReadOnlyList<RunnerControllerTokenSummary> Tokens,
    bool Truncated);

/// <summary>
///     A job a runner is processing or has processed. Name/ref/stage are GitLab-authored text, so
///     every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerJobSummary(
    long Id,
    string? Status,
    string? Stage,
    string? Name,
    string? Ref,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? Duration,
    string? WebUrl);

public sealed record RunnerJobListResult(IReadOnlyList<RunnerJobSummary> Jobs, bool Truncated);

/// <summary>
///     One machine (manager) registered behind a runner registration. Version/platform/architecture
///     are self-reported by the runner process and stored by GitLab, so every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerManagerSummary(
    long Id,
    string? SystemId,
    string? Version,
    string? Revision,
    string? Platform,
    string? Architecture,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ContactedAt,
    string? IpAddress,
    string? Status,
    string? JobExecutionStatus);

public sealed record RunnerManagerListResult(IReadOnlyList<RunnerManagerSummary> Managers, bool Truncated);

/// <summary>
///     A project a project-scoped runner is assigned to. Name/path are GitLab-stored text, so every
///     tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerProjectSummary(long Id, string? Name, string? PathWithNamespace, string? WebUrl);

public sealed record RunnerProjectListResult(IReadOnlyList<RunnerProjectSummary> Projects, bool Truncated);

/// <summary>
///     Confirms a registration/authentication token reset happened and reports the new token's expiry only.
///     Deliberately omits <c>GitLabRunnerToken.Token</c> — named explicitly on CLAUDE.md's closed
///     secret-field list, so the new token's value itself is never returned by this server. Every field is a
///     non-string scalar, so the tools returning this declare the bare record rather than
///     <see cref="GitLabContent.Wrap{T}" />.
/// </summary>
public sealed record RunnerTokenResetResult(DateTimeOffset? TokenExpiresAt);

public sealed record RunnerDeleteResult(long RunnerId, bool Deleted);

public sealed record RunnerProjectUnassignResult(long RunnerId, bool Unassigned);

/// <summary>
///     A runner-controller fleet-management agent. Description is operator-authored free text, so
///     every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record RunnerControllerSummary(
    long Id,
    string? Description,
    string? State,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool? Connected);

public sealed record RunnerControllerListResult(IReadOnlyList<RunnerControllerSummary> Controllers, bool Truncated);

public sealed record RunnerControllerDeleteResult(long RunnerControllerId, bool Deleted);

public sealed record RunnerControllerScopeRemoveResult(long RunnerControllerId, long? RunnerId, bool Removed);

/// <summary>
///     Every field is a non-string scalar, so the tool returning this declares the bare record
///     rather than <see cref="GitLabContent.Wrap{T}" />.
/// </summary>
public sealed record RunnerControllerTokenRevokeResult(long RunnerControllerId, long TokenId, bool Revoked);