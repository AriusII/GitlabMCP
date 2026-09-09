using System.Text.Json.Serialization;
using GitlabMCP.GraphQL.Operations.WorkItems;

namespace GitlabMCP.GraphQL.Serialization;

[JsonSerializable(typeof(GraphQLError))]
[JsonSerializable(typeof(GraphQLErrorLocation))]
[JsonSerializable(typeof(PageInfo))]
[JsonSerializable(typeof(WorkItemNode))]
[JsonSerializable(typeof(WorkItemTypeNode))]
[JsonSerializable(typeof(WorkItemWidgetEntry))]
[JsonSerializable(typeof(WorkItemLabel))]
[JsonSerializable(typeof(WorkItemUser))]
[JsonSerializable(typeof(WorkItemChildConnection))]
[JsonSerializable(typeof(WorkItemState))]
// ListGroupEpics
[JsonSerializable(typeof(GraphQLRequest<ListGroupEpicsVariables>))]
[JsonSerializable(typeof(GraphQLResponse<ListGroupEpicsData>))]
[JsonSerializable(typeof(ListGroupEpicsGroup))]
[JsonSerializable(typeof(WorkItemNodeConnection))]
// GetEpicByIid
[JsonSerializable(typeof(GraphQLRequest<GetEpicByIidVariables>))]
[JsonSerializable(typeof(GraphQLResponse<GetEpicByIidData>))]
[JsonSerializable(typeof(GetEpicByIidGroup))]
// GetEpicWorkItemTypeId
[JsonSerializable(typeof(GraphQLRequest<GetEpicWorkItemTypeIdVariables>))]
[JsonSerializable(typeof(GraphQLResponse<GetEpicWorkItemTypeIdData>))]
[JsonSerializable(typeof(GetEpicWorkItemTypeIdGroup))]
[JsonSerializable(typeof(WorkItemTypeConnection))]
// CreateEpic
[JsonSerializable(typeof(GraphQLRequest<CreateEpicVariables>))]
[JsonSerializable(typeof(GraphQLResponse<CreateEpicData>))]
[JsonSerializable(typeof(WorkItemCreateInput))]
[JsonSerializable(typeof(WorkItemDescriptionWidgetInput))]
[JsonSerializable(typeof(WorkItemLabelsWidgetInput))]
[JsonSerializable(typeof(WorkItemAssigneesWidgetInput))]
[JsonSerializable(typeof(WorkItemStartAndDueDateWidgetInput))]
[JsonSerializable(typeof(WorkItemHierarchyWidgetInput))]
[JsonSerializable(typeof(WorkItemCreatePayload))]
// UpdateEpic
[JsonSerializable(typeof(GraphQLRequest<UpdateEpicVariables>))]
[JsonSerializable(typeof(GraphQLResponse<UpdateEpicData>))]
[JsonSerializable(typeof(WorkItemUpdateInput))]
[JsonSerializable(typeof(WorkItemLabelsUpdateWidgetInput))]
[JsonSerializable(typeof(WorkItemUpdatePayload))]
// DeleteEpic
[JsonSerializable(typeof(GraphQLRequest<DeleteEpicVariables>))]
[JsonSerializable(typeof(GraphQLResponse<DeleteEpicData>))]
[JsonSerializable(typeof(WorkItemDeleteInput))]
[JsonSerializable(typeof(WorkItemDeletePayload))]
[JsonSerializable(typeof(WorkItemDeleteNamespace))]
// AddNoteToEpic
[JsonSerializable(typeof(GraphQLRequest<AddNoteToEpicVariables>))]
[JsonSerializable(typeof(GraphQLResponse<AddNoteToEpicData>))]
[JsonSerializable(typeof(CreateNoteInput))]
[JsonSerializable(typeof(CreateNotePayload))]
[JsonSerializable(typeof(NoteNode))]
// GetEpicChildren
[JsonSerializable(typeof(GraphQLRequest<GetEpicChildrenVariables>))]
[JsonSerializable(typeof(GraphQLResponse<GetEpicChildrenData>))]
[JsonSerializable(typeof(WorkItemHierarchyHolder))]
public sealed partial class GitLabGraphQlJsonContext;