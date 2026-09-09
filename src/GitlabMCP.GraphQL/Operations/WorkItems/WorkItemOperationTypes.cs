namespace GitlabMCP.GraphQL.Operations.WorkItems;

// --- ListGroupEpics ---------------------------------------------------------------------------
public sealed record ListGroupEpicsVariables(string FullPath, int First, string? After, string? Search, string? State);

public sealed record ListGroupEpicsData(ListGroupEpicsGroup? Group);

public sealed record ListGroupEpicsGroup(WorkItemNodeConnection WorkItems);

public sealed record WorkItemNodeConnection(IReadOnlyList<WorkItemNode> Nodes, PageInfo PageInfo);

// --- GetEpicByIid -------------------------------------------------------------------------------
public sealed record GetEpicByIidVariables(string FullPath, string Iid);

public sealed record GetEpicByIidData(GetEpicByIidGroup? Group);

public sealed record GetEpicByIidGroup(WorkItemNode? WorkItem);

// --- GetEpicWorkItemTypeId ------------------------------------------------------------------------
public sealed record GetEpicWorkItemTypeIdVariables(string FullPath);

public sealed record GetEpicWorkItemTypeIdData(GetEpicWorkItemTypeIdGroup? Group);

public sealed record GetEpicWorkItemTypeIdGroup(bool? EpicsEnabled, WorkItemTypeConnection WorkItemTypes);

public sealed record WorkItemTypeConnection(IReadOnlyList<WorkItemTypeNode> Nodes);

// --- CreateEpic -----------------------------------------------------------------------------------
public sealed record CreateEpicVariables(WorkItemCreateInput Input);

public sealed record WorkItemCreateInput(
    string NamespacePath,
    string Title,
    string WorkItemTypeId,
    bool? Confidential,
    WorkItemDescriptionWidgetInput? DescriptionWidget,
    WorkItemLabelsWidgetInput? LabelsWidget,
    WorkItemAssigneesWidgetInput? AssigneesWidget,
    WorkItemStartAndDueDateWidgetInput? StartAndDueDateWidget,
    WorkItemHierarchyWidgetInput? HierarchyWidget);

public sealed record WorkItemDescriptionWidgetInput(string Description);

public sealed record WorkItemLabelsWidgetInput(IReadOnlyList<string>? LabelIds);

public sealed record WorkItemAssigneesWidgetInput(IReadOnlyList<string>? AssigneeIds);

public sealed record WorkItemStartAndDueDateWidgetInput(string? StartDate, string? DueDate);

public sealed record WorkItemHierarchyWidgetInput(string? ParentId);

public sealed record CreateEpicData(WorkItemCreatePayload? WorkItemCreate);

public sealed record WorkItemCreatePayload(WorkItemNode? WorkItem, IReadOnlyList<string> Errors);

// --- UpdateEpic -----------------------------------------------------------------------------------
public sealed record UpdateEpicVariables(WorkItemUpdateInput Input);

public sealed record WorkItemUpdateInput(
    string Id,
    string? Title,
    string? StateEvent,
    WorkItemDescriptionWidgetInput? DescriptionWidget,
    WorkItemLabelsUpdateWidgetInput? LabelsWidget,
    WorkItemAssigneesWidgetInput? AssigneesWidget,
    WorkItemStartAndDueDateWidgetInput? StartAndDueDateWidget,
    WorkItemHierarchyWidgetInput? HierarchyWidget);

public sealed record WorkItemLabelsUpdateWidgetInput(
    IReadOnlyList<string>? AddLabelIds,
    IReadOnlyList<string>? RemoveLabelIds);

public sealed record UpdateEpicData(WorkItemUpdatePayload? WorkItemUpdate);

public sealed record WorkItemUpdatePayload(WorkItemNode? WorkItem, IReadOnlyList<string> Errors);

// --- DeleteEpic -----------------------------------------------------------------------------------
public sealed record DeleteEpicVariables(WorkItemDeleteInput Input);

public sealed record WorkItemDeleteInput(string Id);

public sealed record DeleteEpicData(WorkItemDeletePayload? WorkItemDelete);

public sealed record WorkItemDeletePayload(WorkItemDeleteNamespace? Namespace, IReadOnlyList<string> Errors);

public sealed record WorkItemDeleteNamespace(string Id, string FullPath);

// --- AddNoteToEpic --------------------------------------------------------------------------------
public sealed record AddNoteToEpicVariables(CreateNoteInput Input);

public sealed record CreateNoteInput(string NoteableId, string Body, bool? Internal);

public sealed record AddNoteToEpicData(CreateNotePayload? CreateNote);

public sealed record CreateNotePayload(NoteNode? Note, IReadOnlyList<string> Errors);

public sealed record NoteNode(string Id, string Body, DateTimeOffset CreatedAt);

// --- GetEpicChildren -------------------------------------------------------------------------------
public sealed record GetEpicChildrenVariables(string Id, int First, string? After);

public sealed record GetEpicChildrenData(WorkItemHierarchyHolder? WorkItem);

public sealed record WorkItemHierarchyHolder(string Id, IReadOnlyList<WorkItemWidgetEntry>? Widgets);