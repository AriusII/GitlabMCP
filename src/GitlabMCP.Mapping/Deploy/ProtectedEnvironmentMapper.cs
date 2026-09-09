using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class ProtectedEnvironmentMapper
{
    public static ProtectedEnvironmentSummary ToSummary(GitLabProtectedEnvironment environment)
    {
        return new ProtectedEnvironmentSummary(
            environment.Name,
            environment.DeployAccessLevels?.Select(ToSummary).ToList() ?? [],
            environment.RequiredApprovalCount,
            environment.ApprovalRules?.Select(ToSummary).ToList() ?? []);
    }

    private static DeployAccessLevelSummary ToSummary(GitLabDeployAccessLevel level)
    {
        return new DeployAccessLevelSummary(
            level.Id,
            level.AccessLevel,
            level.AccessLevelDescription,
            level.DeployKeyId,
            level.UserId,
            level.GroupId,
            level.MemberRoleId,
            level.MemberRoleName,
            level.GroupInheritanceType);
    }

    private static ApprovalRuleSummary ToSummary(GitLabProtectedEnvironmentApprovalRule rule)
    {
        return new ApprovalRuleSummary(
            rule.Id,
            rule.UserId,
            rule.GroupId,
            rule.AccessLevel,
            rule.AccessLevelDescription,
            rule.RequiredApprovals,
            rule.GroupInheritanceType);
    }
}