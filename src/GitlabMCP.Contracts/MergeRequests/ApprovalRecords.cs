namespace GitlabMCP.Contracts.MergeRequests;

/// <summary>
///     Project-scoped approval policy switches. <c>Approvers</c>/<c>ApproverGroups</c> are projected to
///     usernames/full paths only — never the underlying <c>GitLabGroup</c> (which carries <c>RunnersToken</c>).
/// </summary>
public sealed record ApprovalConfigurationResult(
    IReadOnlyList<string> Approvers,
    IReadOnlyList<string> ApproverGroups,
    int? ApprovalsBeforeMerge,
    bool? ResetApprovalsOnPush,
    bool? SelectiveCodeOwnerRemovals,
    bool? DisableOverridingApproversPerMergeRequest,
    bool? MergeRequestsAuthorApproval,
    bool? MergeRequestsDisableCommittersApproval,
    bool? RequirePasswordToApprove,
    bool? RequireReauthenticationToApprove);

public sealed record ApprovalRuleSummary(
    long Id,
    string? Name,
    string? RuleType,
    int? ApprovalsRequired,
    IReadOnlyList<string> EligibleApprovers,
    IReadOnlyList<string> Users,
    IReadOnlyList<string> Groups,
    bool? AppliesToAllProtectedBranches,
    IReadOnlyList<string> ProtectedBranches);

public sealed record ApprovalRuleListResult(IReadOnlyList<ApprovalRuleSummary> Rules, bool Truncated);

public sealed record MergeRequestApprovalRuleState(
    long? Id,
    string? Name,
    int? ApprovalsRequired,
    bool? Approved,
    IReadOnlyList<string> ApprovedBy);

/// <summary>
///     Combines the approvers view (<c>GetApprovalsAsync</c>) and the rule-by-rule view (<c>GetApprovalStateAsync</c>
///     ) in one payload.
/// </summary>
public sealed record MergeRequestApprovalsResult(
    bool? Approved,
    bool? UserHasApproved,
    bool? UserCanApprove,
    IReadOnlyList<string> ApprovedBy,
    IReadOnlyList<MergeRequestApprovalRuleState> Rules);

/// <summary>The narrower result of a single approve/unapprove action (just the approvers view, no rule state).</summary>
public sealed record ApprovalActionResult(
    bool? Approved,
    bool? UserHasApproved,
    bool? UserCanApprove,
    IReadOnlyList<string> ApprovedBy);

/// <summary>
///     Result of <c>gitlab_delete_approval_rule</c>. No GitLab-authored string in the payload -- returned bare, not
///     wrapped.
/// </summary>
public sealed record ApprovalRuleDeleteResult(bool Deleted);