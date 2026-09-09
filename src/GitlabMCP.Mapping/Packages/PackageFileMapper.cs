using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>
///     Projects <see cref="GitLabPackageFile" /> into the owned <see cref="PackageFileSummary" /> — never hands back
///     the library DTO.
/// </summary>
public static class PackageFileMapper
{
    public static PackageFileSummary ToSummary(GitLabPackageFile file)
    {
        return new PackageFileSummary(
            file.Id,
            file.PackageId,
            file.FileName,
            file.Size,
            file.CreatedAt,
            file.FileSha256,
            file.FileSha1,
            file.FileMd5);
    }
}