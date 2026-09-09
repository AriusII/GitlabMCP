using GitLab.Client.Models;
using GitlabMCP.Contracts.Code;

namespace GitlabMCP.Mapping.Code;

/// <summary>Projects protected-branch/tag, push-rule and pull-mirror DTOs into the owned "code" records.</summary>
public static class ProtectionMapper
{
    public static AccessLevelSummary ToSummary(GitLabAccessLevel level)
    {
        return new AccessLevelSummary(
            level.AccessLevel,
            level.AccessLevelDescription);
    }

    public static ProtectedBranchSummary ToSummary(GitLabProtectedBranch branch)
    {
        return new ProtectedBranchSummary(
            branch.Id,
            branch.Name,
            branch.AllowForcePush,
            branch.PushAccessLevels?.Select(ToSummary).ToList() ?? [],
            branch.MergeAccessLevels?.Select(ToSummary).ToList() ?? [],
            branch.UnprotectAccessLevels?.Select(ToSummary).ToList() ?? [],
            branch.CodeOwnerApprovalRequired,
            branch.Inherited);
    }

    public static ProtectedTagSummary ToSummary(GitLabProtectedTag tag)
    {
        return new ProtectedTagSummary(
            tag.Name,
            tag.CreateAccessLevels?.Select(ToSummary).ToList() ?? []);
    }

    public static PushRuleSummary ToSummary(GitLabProjectPushRule rule)
    {
        return new PushRuleSummary(
            rule.Id,
            rule.CommitMessageRegex,
            rule.CommitMessageNegativeRegex,
            rule.BranchNameRegex,
            rule.DenyDeleteTag,
            rule.MemberCheck,
            rule.PreventSecrets,
            rule.AuthorEmailRegex,
            rule.FileNameRegex,
            rule.MaxFileSize,
            rule.CommitCommitterCheck,
            rule.CommitCommitterNameCheck,
            rule.RejectUnsignedCommits,
            rule.RejectNonDcoCommits);
    }

    public static PullMirrorSummary ToSummary(GitLabPullMirror mirror)
    {
        return new PullMirrorSummary(
            mirror.Id,
            mirror.UpdateStatus,
            mirror.Url,
            mirror.LastError,
            mirror.LastUpdateAt,
            mirror.LastSuccessfulUpdateAt,
            mirror.Enabled,
            mirror.OnlyMirrorProtectedBranches);
    }
}