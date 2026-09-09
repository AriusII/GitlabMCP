using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class DeployKeyMapper
{
    public static DeployKeySummary ToSummary(GitLabDeployKey key)
    {
        return new DeployKeySummary(
            key.Id,
            key.Title,
            key.FingerprintSha256 ?? key.Fingerprint,
            key.UsageType,
            key.CanPush,
            key.CreatedAt,
            key.ExpiresAt,
            key.ProjectsWithWriteAccess?.PathWithNamespace,
            key.ProjectsWithReadonlyAccess?.PathWithNamespace);
    }
}