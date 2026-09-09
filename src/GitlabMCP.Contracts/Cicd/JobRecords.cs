namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     Job/artifact-entry projection records (domain "cicd"). Name/Stage/Ref/Status/FailureReason and the
///     triggering user's username are GitLab-authored or GitLab-controlled, so every tool returning one of
///     these wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record JobSummary(
    long Id,
    string? Status,
    string? Stage,
    string? Name,
    string? Ref,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? Duration,
    string? WebUrl,
    bool? AllowFailure,
    double? Coverage,
    string? FailureReason,
    string? TriggeredByUsername,
    long? PipelineId,
    string? CommitShortId,
    long? DownstreamPipelineId);

public sealed record JobListResult(IReadOnlyList<JobSummary> Jobs, bool Truncated);

/// <summary>One entry (file or directory) inside a job's artifacts archive (<c>gitlab_list_job_artifacts</c>).</summary>
public sealed record JobArtifactEntrySummary(
    string? Name,
    string? Path,
    string? Type,
    long? Size,
    string? Mode);

public sealed record JobArtifactEntryListResult(IReadOnlyList<JobArtifactEntrySummary> Entries, bool Truncated);

/// <summary>
///     Result of <c>gitlab_delete_all_project_artifacts</c>. Carries no GitLab-authored text at all (just a
///     confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4.
/// </summary>
public sealed record DeleteAllArtifactsResult(bool Deleted);

/// <summary>
///     A bounded, base64-encoded download of a job's whole artifacts archive (<c>gitlab_download_job_artifacts</c>,
///     <c>GitLabFileResponse</c> from either <c>IJobArtifactsClient.DownloadAsync</c> or
///     <c>DownloadForRefAsync</c>) — mirrors <c>InfraTools</c>' attestation-download shape: the tool refuses
///     via <c>McpException</c> rather than silently truncating a binary archive past its byte limit.
///     <see cref="FileName" /> is GitLab-controlled (normally <c>artifacts.zip</c>), so the tool wraps via
///     <see cref="GitLabContent" />.
/// </summary>
public sealed record JobArtifactArchiveDownload(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

/// <summary>
///     A bounded, base64-encoded download of one file out of a job's artifacts archive
///     (<c>gitlab_get_job_artifact_file</c>, <c>GitLabFileResponse</c> from either
///     <c>IJobArtifactsClient.DownloadFileAsync</c> or <c>DownloadFileForRefAsync</c>). Same shape and same
///     refuse-rather-than-truncate policy as <see cref="JobArtifactArchiveDownload" />, just for one file
///     instead of the whole archive.
/// </summary>
public sealed record JobArtifactFileDownload(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

/// <summary>
///     Result of <c>gitlab_delete_job_artifacts</c>. Carries no GitLab-authored text at all (just a
///     confirmation flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4 — same shape as
///     <see cref="DeleteAllArtifactsResult" />, just for one job instead of the whole project.
/// </summary>
public sealed record JobArtifactsDeleteResult(bool Deleted);