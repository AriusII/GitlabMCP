namespace GitlabMCP.Contracts.Deploy;

/// <summary>
///     A deployment freeze window: a cron start/end pair and time zone during which GitLab blocks
///     new deployments to the project. FreezeStart/FreezeEnd/CronTimezone are project-supplied strings
///     stored and echoed back by GitLab, so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record FreezePeriodSummary(
    long Id,
    string? FreezeStart,
    string? FreezeEnd,
    string? CronTimezone,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record FreezePeriodListResult(IReadOnlyList<FreezePeriodSummary> FreezePeriods, bool Truncated);

/// <summary>Confirms a freeze period was deleted. All-scalar bare-record result.</summary>
public sealed record FreezePeriodDeleteResult(long FreezePeriodId, bool Deleted);