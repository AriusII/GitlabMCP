namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     Secure file metadata only — never the file contents (the client's <c>ListAsync</c> never
///     fetches them either). Name is a caller-chosen filename stored and returned by GitLab, so the tool
///     returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record SecureFileSummary(
    long Id,
    string? Name,
    string? Checksum,
    string? ChecksumAlgorithm,
    string? FileExtension,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record SecureFileListResult(IReadOnlyList<SecureFileSummary> SecureFiles, bool Truncated);

/// <summary>
///     Bounded, base64-encoded download of a secure file's raw contents (<c>gitlab_download_secure_file</c>)
///     — mirrors <c>CicdTools</c>' job-artifact-file and <c>InfraTools</c>' attestation download shape: the
///     tool refuses via <c>McpException</c> rather than silently truncating binary content past its byte
///     limit. <see cref="FileName" /> is GitLab-stored text, so the tool wraps via <c>GitLabContent</c>. The
///     content itself is a live secret (a certificate, provisioning profile or keystore) even though it is
///     not on CLAUDE.md's closed secret-field list — callers must handle and store it accordingly.
/// </summary>
public sealed record SecureFileDownload(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

/// <summary>
///     Result of <c>gitlab_delete_secure_file</c>. Carries no GitLab-authored text at all (just a
///     confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record SecureFileDeleteResult(long SecureFileId, bool Deleted);