using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

public static class SecureFileMapper
{
    public static SecureFileSummary ToSummary(GitLabSecureFile file)
    {
        return new SecureFileSummary(
            file.Id,
            file.Name,
            file.Checksum,
            file.ChecksumAlgorithm,
            file.FileExtension,
            file.CreatedAt,
            file.ExpiresAt);
    }
}