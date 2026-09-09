namespace GitlabMCP.Contracts.Cicd;

/// <summary>
///     Pipeline projection records (domain "cicd"). Ref/Sha/Status/Source/Name and the triggering user's
///     username are all GitLab-authored or GitLab-controlled text, so every tool returning one of these
///     wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record PipelineSummary(
    long Id,
    long? Iid,
    long? ProjectId,
    string? Sha,
    string? Ref,
    string? Status,
    string? Source,
    string? Name,
    string? WebUrl,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? Duration,
    double? Coverage,
    string? TriggeredByUsername);

public sealed record PipelineListResult(IReadOnlyList<PipelineSummary> Pipelines, bool Truncated);

/// <summary>A cron-triggered pipeline schedule (<c>gitlab_create_pipeline_schedule</c>).</summary>
public sealed record PipelineScheduleSummary(
    long Id,
    string? Description,
    string? Ref,
    string? Cron,
    string? CronTimezone,
    DateTimeOffset? NextRunAt,
    bool? Active,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? OwnerUsername,
    long? LastPipelineId,
    string? LastPipelineStatus);

public sealed record PipelineScheduleListResult(IReadOnlyList<PipelineScheduleSummary> Schedules, bool Truncated);

/// <summary>
///     Result of <c>gitlab_delete_pipeline</c>. Carries no GitLab-authored text at all (just a confirmation
///     flag), so this is the bare, unwrapped payload per CLAUDE.md rule 4 — same shape as
///     <see cref="DeleteAllArtifactsResult" /> in <c>JobRecords.cs</c>.
/// </summary>
public sealed record PipelineDeleteResult(bool Deleted);

/// <summary>
///     Result of <c>gitlab_delete_pipeline_schedule</c>. Bare, unwrapped payload — see
///     <see cref="PipelineDeleteResult" />.
/// </summary>
public sealed record PipelineScheduleDeleteResult(bool Deleted);

/// <summary>
///     Result of <c>gitlab_run_pipeline_schedule</c>. GitLab acknowledges the trigger with a bare message
///     rather than the pipeline it created, so there is nothing GitLab-authored to return — bare, unwrapped
///     payload per CLAUDE.md rule 4, same shape as <see cref="PipelineDeleteResult" />. Call
///     <c>gitlab_list_pipeline_schedule_pipelines</c> to find the resulting run.
/// </summary>
public sealed record PipelineScheduleRunResult(bool Triggered);