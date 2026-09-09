using GitLab.Client.Models;
using GitlabMCP.Contracts.Deploy;

namespace GitlabMCP.Mapping.Deploy;

/// <summary>
///     Projects <see cref="GitLabRelease" /> into the owned <see cref="ReleaseSummary" /> — never
///     hands back the library's DTO (it also carries assets/links/evidence this domain's tools don't need).
/// </summary>
public static class ReleaseMapper
{
    public static ReleaseSummary ToSummary(GitLabRelease release)
    {
        return new ReleaseSummary(
            release.TagName,
            release.Name,
            release.Description,
            release.ReleasedAt,
            release.Author?.Username,
            release.UpcomingRelease,
            release.Milestones?.Select(static m => m.Title).ToList() ?? [],
            release.WebUrl?.ToString());
    }

    public static ReleaseEvidenceSummary ToEvidenceSummary(GitLabReleaseEvidence evidence)
    {
        return new ReleaseEvidenceSummary(
            evidence.Sha,
            evidence.Filepath?.ToString(),
            evidence.CollectedAt);
    }

    public static ReleaseLinkSummary ToLinkSummary(GitLabReleaseLink link)
    {
        return new ReleaseLinkSummary(
            link.Id,
            link.Name,
            link.Url?.ToString(),
            link.DirectAssetUrl?.ToString(),
            link.LinkType?.ToString(),
            link.External);
    }
}