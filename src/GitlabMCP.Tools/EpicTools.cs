using System.ComponentModel;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Epics;
using GitlabMCP.GraphQL.Operations.WorkItems;
using GitlabMCP.Mapping.Epics;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Epic/work-item tools (DEC-011=C, DEC-020/021) — built on the hand-rolled GraphQL client, since
///     <c>GitLab.Client</c> has no <c>IEpicsClient</c>/<c>IWorkItemsClient</c>. Every tool string here
///     originates from GitLab (title, description, labels, ...), so every tool wraps via
///     <see cref="GitLabContent" /> — DEC-007.
/// </summary>
[McpServerToolType]
public sealed class EpicTools(GitLabWorkItemsClient workItems)
{
    private const int MaxLimit = 100;

    [McpServerTool(Name = "gitlab_list_epics", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists epics (work items of type Epic) in a GitLab group. Requires GitLab Premium or Ultimate on that group.")]
    public async Task<CallToolResult> ListEpicsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Optional free-text search over epic titles/descriptions.")]
        string? search = null,
        [Description("Optional state filter: \"opened\" or \"closed\". Omit for both.")]
        string? state = null,
        [Description("Maximum epics to return (1-100).")]
        int limit = 20,
        [Description("Pagination cursor from a previous call's result, or omitted for the first page.")]
        string? after = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var gqlState = state?.ToLowerInvariant() switch
        {
            "opened" => "opened",
            "closed" => "closed",
            null or "" => null,
            _ => throw new McpException("state must be \"opened\", \"closed\", or omitted.")
        };

        var connection = await workItems.ListGroupEpicsAsync(group, limit, after, search, gqlState, cancellationToken);

        var epics = connection.Nodes.Select(EpicMapper.ToSummary).ToList();
        var result = new EpicListResult(epics, connection.PageInfo.HasNextPage, connection.PageInfo.EndCursor);
        return GitLabContent.Wrap(result, "groups/:id/workItems?types=EPIC");
    }

    [McpServerTool(Name = "gitlab_get_epic", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one epic by its group and iid, including description, labels, assignees, dates and hierarchy.")]
    public async Task<CallToolResult> GetEpicAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The epic's iid (the number shown in the GitLab UI), as a string.")]
        string iid,
        CancellationToken cancellationToken)
    {
        var node = await workItems.GetEpicByIidAsync(group, iid, cancellationToken);
        return GitLabContent.Wrap(EpicMapper.ToDetail(node), "groups/:id/workItems/:iid");
    }

    [McpServerTool(Name = "gitlab_create_epic", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new epic in a GitLab group. Requires GitLab Premium or Ultimate on that group.")]
    public async Task<CallToolResult> CreateEpicAsync(
        [Description("The group's numeric id or full path to create the epic in.")]
        string group,
        [Description("The epic's title.")] string title,
        [Description("Optional description in GitLab-flavored Markdown.")]
        string? description = null,
        [Description("Optional start date, ISO 8601 (YYYY-MM-DD).")]
        string? startDate = null,
        [Description("Optional due date, ISO 8601 (YYYY-MM-DD).")]
        string? dueDate = null,
        [Description(
            "Optional parent epic's global id (from a prior gitlab_get_epic result), to create this as a child.")]
        string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var typeId = await workItems.ResolveEpicWorkItemTypeIdAsync(group, cancellationToken);

        var input = new WorkItemCreateInput(
            group,
            title,
            typeId,
            null,
            description is null ? null : new WorkItemDescriptionWidgetInput(description),
            null,
            null,
            startDate is null && dueDate is null ? null : new WorkItemStartAndDueDateWidgetInput(startDate, dueDate),
            parentId is null ? null : new WorkItemHierarchyWidgetInput(parentId));

        var node = await workItems.CreateEpicAsync(input, cancellationToken);
        return GitLabContent.Wrap(EpicMapper.ToSummary(node), "groups/:id/workItems (create)");
    }

    [McpServerTool(Name = "gitlab_update_epic", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Updates an epic's title, description, state, or dates. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateEpicAsync(
        [Description("The epic's global id (gid://gitlab/WorkItem/... — from gitlab_get_epic or gitlab_list_epics).")]
        string workItemId,
        [Description("New title, or omitted to leave unchanged.")]
        string? title = null,
        [Description("New description, or omitted to leave unchanged.")]
        string? description = null,
        [Description("\"close\" or \"reopen\", or omitted to leave the state unchanged.")]
        string? stateEvent = null,
        [Description("New start date, ISO 8601, or omitted to leave unchanged.")]
        string? startDate = null,
        [Description("New due date, ISO 8601, or omitted to leave unchanged.")]
        string? dueDate = null,
        CancellationToken cancellationToken = default)
    {
        var gqlStateEvent = stateEvent?.ToLowerInvariant() switch
        {
            "close" => "CLOSE",
            "reopen" => "REOPEN",
            null or "" => null,
            _ => throw new McpException("stateEvent must be \"close\", \"reopen\", or omitted.")
        };

        var input = new WorkItemUpdateInput(
            workItemId,
            title,
            gqlStateEvent,
            description is null ? null : new WorkItemDescriptionWidgetInput(description),
            null,
            null,
            startDate is null && dueDate is null ? null : new WorkItemStartAndDueDateWidgetInput(startDate, dueDate),
            null);

        var node = await workItems.UpdateEpicAsync(input, cancellationToken);
        return GitLabContent.Wrap(EpicMapper.ToSummary(node), "workItems/:id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_epic", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes an epic. This cannot be undone.")]
    public async Task<CallToolResult> DeleteEpicAsync(
        [Description("The epic's global id (gid://gitlab/WorkItem/...).")]
        string workItemId,
        CancellationToken cancellationToken)
    {
        var fullPath = await workItems.DeleteEpicAsync(workItemId, cancellationToken);
        return GitLabContent.Wrap(new EpicDeleteResult(fullPath), "workItems/:id (delete)");
    }

    [McpServerTool(Name = "gitlab_add_epic_note", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Adds a comment (note) to an epic.")]
    public async Task<CallToolResult> AddEpicNoteAsync(
        [Description("The epic's global id (gid://gitlab/WorkItem/...).")]
        string workItemId,
        [Description("The comment body, in GitLab-flavored Markdown.")]
        string body,
        [Description("If true, the note is internal-only (not visible to non-members). Default false.")]
        bool @internal = false,
        CancellationToken cancellationToken = default)
    {
        var note = await workItems.AddNoteToEpicAsync(workItemId, body, @internal, cancellationToken);
        return GitLabContent.Wrap(new EpicNoteResult(note.Id, note.CreatedAt), "workItems/:id/notes (create)");
    }

    [McpServerTool(Name = "gitlab_list_epic_children", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the child work items (sub-epics or issues) directly under an epic.")]
    public async Task<CallToolResult> ListEpicChildrenAsync(
        [Description("The epic's global id (gid://gitlab/WorkItem/...).")]
        string workItemId,
        [Description("Maximum children to return (1-100).")]
        int limit = 20,
        [Description("Pagination cursor from a previous call's result, or omitted for the first page.")]
        string? after = null,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var children = await workItems.GetEpicChildrenAsync(workItemId, limit, after, cancellationToken);
        var result = new EpicListResult(
            children.Nodes.Select(EpicMapper.ToSummary).ToList(),
            children.PageInfo.HasNextPage,
            children.PageInfo.EndCursor);
        return GitLabContent.Wrap(result, "workItems/:id (hierarchy.children)");
    }
}