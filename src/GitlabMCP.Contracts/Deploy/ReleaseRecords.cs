namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Release projection (DEC-011=C). Title/description/author/milestone titles all originate on
///     GitLab, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ReleaseSummary(
    string? TagName,
    string? Name,
    string? Description,
    DateTimeOffset? ReleasedAt,
    string? AuthorUsername,
    bool? UpcomingRelease,
    IReadOnlyList<string> MilestoneTitles,
    string? WebUrl);

public sealed record ReleaseListResult(IReadOnlyList<ReleaseSummary> Releases, bool Truncated);

/// <summary>
///     <see cref="TagName" /> is the caller-supplied identifier being confirmed, not GitLab-authored
///     text, so this is the rare all-scalar bare-record result (mcp-tool-authoring Step 4).
/// </summary>
public sealed record ReleaseDeleteResult(string TagName, bool Deleted);

/// <summary>
///     One compliance evidence snapshot collected for a release. <see cref="FilepathUrl" /> is a
///     GitLab-hosted URL, so every tool returning a record containing this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record ReleaseEvidenceSummary(string? Sha, string? FilepathUrl, DateTimeOffset? CollectedAt);

/// <summary>
///     Result of collecting a fresh evidence snapshot: the release's full, updated evidence list.
///     Wraps via <see cref="GitLabContent" /> — <see cref="ReleaseEvidenceSummary" /> carries GitLab text.
/// </summary>
public sealed record GenerateReleaseEvidenceResult(string? TagName, IReadOnlyList<ReleaseEvidenceSummary> Evidences);

/// <summary>
///     One downloadable asset link attached to a release. Title/URL/type all originate on GitLab,
///     so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ReleaseLinkSummary(
    long Id,
    string? Name,
    string? Url,
    string? DirectAssetUrl,
    string? LinkType,
    bool? External);

public sealed record ReleaseLinkListResult(IReadOnlyList<ReleaseLinkSummary> Links, bool Truncated);

/// <summary>Confirms one release asset link was deleted. All-scalar bare-record result.</summary>
public sealed record ReleaseLinkDeleteResult(long LinkId, bool Deleted);

/// <summary>
///     The project's latest release, resolved without knowing its tag name in advance. Parsed out
///     of GitLab's undocumented permalink JSON body (see <c>IReleasesClient.GetLatestReleaseAsync</c>'s own
///     remarks) rather than the library's typed <see cref="ReleaseSummary" /> shape. Every field originates
///     on GitLab, so every tool returning a record containing this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record LatestReleaseSummary(
    string? TagName,
    string? Name,
    string? Description,
    DateTimeOffset? ReleasedAt,
    string? AuthorUsername,
    bool? UpcomingRelease,
    string? WebUrl);

/// <summary>
///     Metadata-only summary of a file resolved relative to the latest release (e.g. one of its
///     assets) — never the file's content. <see cref="FileName" />/<see cref="ContentType" /> are
///     server-reported, so every tool returning a record containing this wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record LatestReleaseAssetSummary(
    string? FileName,
    string? ContentType,
    long? ContentLength,
    int StatusCode);

/// <summary>
///     Exactly one of <see cref="Release" /> or <see cref="Asset" /> is set, depending on whether the
///     caller asked to resolve the release itself or a path relative to it.
/// </summary>
public sealed record LatestReleaseResult(LatestReleaseSummary? Release, LatestReleaseAssetSummary? Asset);

/// <summary>
///     Bounded, base64-encoded download of one asset file attached to a release
///     (<c>gitlab_download_release_asset</c>) — mirrors <c>CicdTools</c>' job-artifact-file and
///     <c>InfraTools</c>' attestation download shape: the tool refuses via <c>McpException</c> rather than
///     silently truncating binary content past its byte limit. <see cref="FileName" /> is GitLab-reported, so
///     the tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ReleaseAssetDownload(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);