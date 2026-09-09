namespace GitlabMCP.Contracts.Planning;

/// <summary>
///     A unified projection of every resource-event family <c>IResourceEventsClient</c> exposes (label,
///     state, milestone, iteration, weight) across issues, merge requests and epics — one shared record so
///     <c>gitlab_list_resource_events</c> can dispatch to any of the ten underlying methods and still declare
///     a single return shape. Only the fields relevant to the requested family are non-null.
/// </summary>
public sealed record ResourceEventSummary(
    long Id,
    string? Action,
    string? ResourceType,
    long? ResourceId,
    DateTimeOffset? CreatedAt,
    string? UserUsername,
    string? LabelName,
    string? MilestoneTitle,
    string? IterationTitle,
    int? Weight,
    string? State);

public sealed record ResourceEventListResult(IReadOnlyList<ResourceEventSummary> Events, bool Truncated);