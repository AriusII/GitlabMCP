using GitLab.Client.Models;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Mapping.Packages;

/// <summary>
///     Projects <see cref="GitLabPackage" /> into the owned <see cref="PackageSummary" /> — never hands back the
///     library DTO.
/// </summary>
public static class PackageMapper
{
    public static PackageSummary ToSummary(GitLabPackage package)
    {
        return new PackageSummary(
            package.Id,
            package.Name,
            package.Version,
            package.PackageType?.ToString(),
            package.Status?.ToString(),
            package.CreatedAt,
            package.ProjectId,
            package.ProjectPath);
    }

    /// <summary>Projects <see cref="GitLabPackage" /> into the fuller, owned <see cref="PackageDetail" />.</summary>
    public static PackageDetail ToDetail(GitLabPackage package)
    {
        return new PackageDetail(
            package.Id,
            package.Name,
            package.Version,
            package.PackageType?.ToString(),
            package.Status?.ToString(),
            package.CreatedAt,
            package.LastDownloadedAt,
            package.CreatorId,
            package.ProjectId,
            package.ProjectPath,
            package.Tags,
            package.Pipeline?.Id,
            package.Pipeline?.Status,
            package.Pipeline?.Ref);
    }
}