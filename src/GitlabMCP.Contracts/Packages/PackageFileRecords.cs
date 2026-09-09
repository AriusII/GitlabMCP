namespace GitlabMCP.Contracts.Packages;

/// <summary>
///     One published file within a package version (<c>GitLabPackageFile</c>). The checksums are
///     verification hashes, not credentials, so they are safe to return. <see cref="FileName" /> is
///     GitLab-authored (round-tripped from whatever the publisher named it), so every tool returning this
///     wraps via <see cref="GitlabMCP.Contracts.GitLabContent" />.
/// </summary>
public sealed record PackageFileSummary(
    long Id,
    long PackageId,
    string? FileName,
    long? Size,
    DateTimeOffset? CreatedAt,
    string? FileSha256,
    string? FileSha1,
    string? FileMd5);

public sealed record PackageFileListResult(IReadOnlyList<PackageFileSummary> Files, bool Truncated);

/// <summary>
///     All-scalar confirmation of a file delete — no GitLab-authored string, so the tool returns this bare
///     (unwrapped).
/// </summary>
public sealed record PackageFileDeleteResult(long PackageId, long PackageFileId, bool Deleted);

/// <summary>
///     A bounded, base64-encoded download of one generic package file (<c>GitLabFileResponse</c>) —
///     mirrors <c>InfraTools</c>' attestation-download shape: the tool refuses via <c>McpException</c>
///     rather than silently truncating a binary artifact past its byte limit.
/// </summary>
public sealed record GenericPackageFileDownload(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);