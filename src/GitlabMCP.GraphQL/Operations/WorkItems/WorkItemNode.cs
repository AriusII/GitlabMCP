using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitlabMCP.GraphQL.Operations.WorkItems;

/// <summary>
///     The work item node shape common to every operation that returns one — an epic IS a work item whose
///     <see cref="WorkItemType" /> is "Epic".
/// </summary>
public sealed record WorkItemNode(
    string Id,
    string Iid,
    string Title,
    WorkItemState State,
    WorkItemTypeNode WorkItemType,
    string? WebUrl,
    IReadOnlyList<WorkItemWidgetEntry>? Widgets);

public sealed record WorkItemTypeNode(string Id, string Name);

/// <summary>
///     One element of GitLab's polymorphic <c>widgets</c> array. GraphQL merges every requested inline
///     fragment's fields into the same flat JSON object per element; only the fields matching that element's
///     actual widget <see cref="Type" /> are populated, the rest are absent (null here). Flattening onto one
///     record — rather than one CLR type per widget kind — keeps the type count bounded regardless of how
///     many widget kinds a future query adds.
/// </summary>
public sealed record WorkItemWidgetEntry(
    string Type,
    string? Description,
    IReadOnlyList<WorkItemLabel>? Labels,
    IReadOnlyList<WorkItemUser>? Assignees,
    string? StartDate,
    string? DueDate,
    string? HealthStatus,
    bool? HasParent,
    bool? HasChildren,
    WorkItemNode? Parent,
    WorkItemChildConnection? Children);

public sealed record WorkItemLabel(string Id, string Title, string? Color);

public sealed record WorkItemUser(string Id, string Username, string? Name);

public sealed record WorkItemChildConnection(IReadOnlyList<WorkItemNode> Nodes, PageInfo PageInfo);

/// <summary>GitLab's wire values are "OPEN"/"CLOSED" — a naming policy, not the bare generic converter, is required.</summary>
[JsonConverter(typeof(WorkItemStateJsonConverter))]
public enum WorkItemState
{
    Open,
    Closed
}

public sealed class WorkItemStateJsonConverter()
    : JsonStringEnumConverter<WorkItemState>(JsonNamingPolicy.SnakeCaseUpper);