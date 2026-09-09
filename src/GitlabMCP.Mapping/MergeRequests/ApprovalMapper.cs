using GitLab.Client.Models;
using GitlabMCP.Contracts.MergeRequests;

namespace GitlabMCP.Mapping.MergeRequests;

/// <summary>
///     Projects approval-related DTOs into owned records. Approver groups are projected to
///     <c>GitLabGroup.FullPath</c> only — never the group object itself, which carries <c>RunnersToken</c>
///     (a live credential; see CLAUDE.md's secret-projection closed list).
/// </summary>
public static class ApprovalMapper
{
    public static ApprovalConfigurationResult ToResult(GitLabProjectApprovalConfiguration config)
    {
        return new ApprovalConfigurationResult(
            config.Approvers?.Select(static a => a.User?.Username).Where(static u => u is not null)
                .Select(static u => u!).ToList() ?? [],
            config.ApproverGroups?.Select(static g => g.Group?.FullPath).Where(static p => p is not null)
                .Select(static p => p!).ToList() ?? [],
            config.ApprovalsBeforeMerge,
            config.ResetApprovalsOnPush,
            config.SelectiveCodeOwnerRemovals,
            config.DisableOverridingApproversPerMergeRequest,
            config.MergeRequestsAuthorApproval,
            config.MergeRequestsDisableCommittersApproval,
            config.RequirePasswordToApprove,
            config.RequireReauthenticationToApprove);
    }

    public static ApprovalRuleSummary ToSummary(GitLabApprovalRule rule)
    {
        return new ApprovalRuleSummary(
            rule.Id,
            rule.Name,
            rule.RuleType?.ToString(),
            rule.ApprovalsRequired,
            rule.EligibleApprovers?.Select(static u => u.Username).Where(static u => u is not null)
                .Select(static u => u!).ToList() ?? [],
            rule.Users?.Select(static u => u.Username).Where(static u => u is not null).Select(static u => u!)
                .ToList() ?? [],
            rule.Groups?.Select(static g => g.FullPath).Where(static p => p is not null).Select(static p => p!)
                .ToList() ?? [],
            rule.AppliesToAllProtectedBranches,
            rule.ProtectedBranches?.Select(static b => b.Name).Where(static n => n is not null).Select(static n => n!)
                .ToList() ?? []);
    }

    public static MergeRequestApprovalRuleState ToRuleState(GitLabMergeRequestApprovalRule rule)
    {
        return new MergeRequestApprovalRuleState(
            rule.Id,
            rule.Name,
            rule.ApprovalsRequired,
            rule.Approved,
            rule.ApprovedBy?.Select(static u => u.Username).Where(static u => u is not null).Select(static u => u!)
                .ToList() ?? []);
    }

    public static MergeRequestApprovalsResult ToResult(GitLabMergeRequestApprovals approvals,
        GitLabMergeRequestApprovalState state)
    {
        return new MergeRequestApprovalsResult(
            approvals.Approved,
            approvals.UserHasApproved,
            approvals.UserCanApprove,
            approvals.ApprovedBy?.Select(static a => a.User?.Username).Where(static u => u is not null)
                .Select(static u => u!).ToList() ?? [],
            state.Rules?.Select(ToRuleState).ToList() ?? []);
    }

    public static ApprovalActionResult ToActionResult(GitLabMergeRequestApprovals approvals)
    {
        return new ApprovalActionResult(
            approvals.Approved,
            approvals.UserHasApproved,
            approvals.UserCanApprove,
            approvals.ApprovedBy?.Select(static a => a.User?.Username).Where(static u => u is not null)
                .Select(static u => u!).ToList() ?? []);
    }
}