namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     One deploy-access entry on a protected environment: a user, a group, a deploy key, a
///     member role, or a bare access level allowed to deploy. <see cref="AccessLevelDescription" /> and
///     <see cref="MemberRoleName" /> are GitLab-rendered text, so every tool returning a record containing
///     this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record DeployAccessLevelSummary(
    long? Id,
    int? AccessLevel,
    string? AccessLevelDescription,
    long? DeployKeyId,
    long? UserId,
    long? GroupId,
    long? MemberRoleId,
    string? MemberRoleName,
    int? GroupInheritanceType);

/// <summary>
///     One approval rule on a protected environment: a user, group or access level that must
///     sign off on a deployment, and how many of its approvals are required.
///     <see cref="AccessLevelDescription" /> is GitLab-rendered text, so every tool returning a record
///     containing this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ApprovalRuleSummary(
    long? Id,
    long? UserId,
    long? GroupId,
    int? AccessLevel,
    string? AccessLevelDescription,
    int? RequiredApprovals,
    int? GroupInheritanceType);

/// <summary>
///     A protected environment (project scope) or protected deployment tier (group scope): who
///     may deploy to it, and who must approve. Every tool returning this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record ProtectedEnvironmentSummary(
    string? Name,
    IReadOnlyList<DeployAccessLevelSummary> DeployAccessLevels,
    int? RequiredApprovalCount,
    IReadOnlyList<ApprovalRuleSummary> ApprovalRules);

public sealed record ProtectedEnvironmentListResult(
    IReadOnlyList<ProtectedEnvironmentSummary> Environments,
    bool Truncated);

/// <summary>
///     Confirms an environment (project scope) or deployment tier (group scope) was unprotected.
///     <see cref="Name" /> is the caller-supplied identifier being confirmed, not GitLab-authored text, so
///     this is the rare all-scalar bare-record result (mcp-tool-authoring Step 4) — matching the precedent
///     set by e.g. <c>LabelDeleteResult</c>/<c>WikiPageDeleteResult</c> elsewhere in this codebase.
/// </summary>
public sealed record EnvironmentUnprotectResult(string Name, bool Unprotected);