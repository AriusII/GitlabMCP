namespace GitlabMCP.Contracts.Search;

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabActiveContextCollectionDetail</c>. <c>Name</c> is a
///     GitLab-assigned collection identifier (e.g. <c>gitlab_active_context_code</c>) and <c>Options</c>
///     arrives as raw <c>JsonElement</c> (surfaced here as its JSON text, not re-typed), so this is
///     GitLab-served text and the tool wraps via <see cref="GitLabContent" />. <see cref="OptionsJson" /> is an
///     unbounded raw-JSON passthrough, so it is capped like the raw-JSON text
///     <c>gitlab_list_search_migrations</c> returns; <see cref="OptionsJsonTruncated" /> says whether it was cut.
/// </summary>
public sealed record ActiveContextCollectionResult(
    long? Id,
    string? Name,
    long? ConnectionId,
    string? OptionsJson,
    bool OptionsJsonTruncated,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabActiveContextConnection</c>. <see cref="Name" />/
///     <see cref="AdapterClass" />/<see cref="Prefix" /> are GitLab-configured connection metadata
///     (instance-administrator authored), so every tool returning this wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ActiveContextConnectionSummary(
    long? Id,
    string? Name,
    string? AdapterClass,
    string? Prefix,
    bool? Active,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ActiveContextConnectionListResult(
    IReadOnlyList<ActiveContextConnectionSummary> Connections,
    bool Truncated);

/// <summary>
///     Projection of <c>GitLab.Client.Models.GitLabActiveContextCodeEnabledNamespace</c>. <see cref="State" />
///     is GitLab-reported text ("pending"/"ready"), so the tool wraps via <see cref="GitLabContent" />.
/// </summary>
public sealed record ActiveContextNamespaceStateResult(
    long? Id,
    long? NamespaceId,
    long? ConnectionId,
    string? State,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
///     Confirms <c>gitlab_clear_active_context_dead_queue</c> ran. GitLab's <c>ClearDeadQueueAsync</c>
///     answers with no response body (a bare <c>Task</c>), so there is no GitLab-authored field to project
///     here — a bare (non-wrapped) record is correct per DEC-007.
/// </summary>
public sealed record ActiveContextDeadQueueClearResult(bool Cleared);

/// <summary>
///     Confirms <c>gitlab_replay_active_context_dead_queue</c> ran. <see cref="Queue" /> echoes back the
///     caller-supplied target queue name — it is the MCP request's own input, never text GitLab returned —
///     and GitLab's <c>ReplayDeadQueueAsync</c> answers with no response body (a bare <c>Task</c>), so a bare
///     (non-wrapped) record is correct per DEC-007.
/// </summary>
public sealed record ActiveContextDeadQueueReplayResult(string Queue, bool Replayed);