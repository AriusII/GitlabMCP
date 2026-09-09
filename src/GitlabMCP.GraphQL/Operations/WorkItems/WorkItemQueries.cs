namespace GitlabMCP.GraphQL.Operations.WorkItems;

/// <summary>
///     GraphQL query/mutation text, verified field-by-field against a live GitLab.com schema introspection
///     (docs/architecture.md §The GraphQL path). Every widget field selected here has a matching property on
///     <see cref="WorkItemWidgetEntry" /> — adding a field here without adding it there silently drops data,
///     not a compile error, since GraphQL responses are just JSON.
/// </summary>
internal static class WorkItemQueries
{
    private const string WidgetFields = """
                                        widgets {
                                          type
                                          ... on WorkItemWidgetDescription { description }
                                          ... on WorkItemWidgetLabels { labels(first: 50) { nodes { id title color } } }
                                          ... on WorkItemWidgetAssignees { assignees(first: 50) { nodes { id username name } } }
                                          ... on WorkItemWidgetStartAndDueDate { startDate dueDate }
                                          ... on WorkItemWidgetHealthStatus { healthStatus }
                                          ... on WorkItemWidgetHierarchy {
                                            hasParent
                                            hasChildren
                                            parent { id iid title state webUrl workItemType { id name } }
                                          }
                                        }
                                        """;

    public const string ListGroupEpics = $$"""
                                           query ListGroupEpics($fullPath: ID!, $first: Int!, $after: String, $search: String, $state: IssuableState) {
                                             group(fullPath: $fullPath) {
                                               workItems(types: [EPIC], search: $search, state: $state, sort: CREATED_DESC, first: $first, after: $after) {
                                                 pageInfo { hasNextPage hasPreviousPage startCursor endCursor }
                                                 nodes {
                                                   id iid title state webUrl
                                                   workItemType { id name }
                                                   {{WidgetFields}}
                                                 }
                                               }
                                             }
                                           }
                                           """;

    public const string GetEpicByIid = $$"""
                                         query GetEpicByIid($fullPath: ID!, $iid: String!) {
                                           group(fullPath: $fullPath) {
                                             workItem(iid: $iid) {
                                               id iid title state webUrl
                                               workItemType { id name }
                                               {{WidgetFields}}
                                             }
                                           }
                                         }
                                         """;

    public const string GetEpicWorkItemTypeId = """
                                                query GetEpicWorkItemTypeId($fullPath: ID!) {
                                                  group(fullPath: $fullPath) {
                                                    epicsEnabled
                                                    workItemTypes(name: EPIC) { nodes { id name } }
                                                  }
                                                }
                                                """;

    public const string CreateEpic = """
                                     mutation CreateEpic($input: WorkItemCreateInput!) {
                                       workItemCreate(input: $input) {
                                         workItem { id iid title webUrl workItemType { id name } }
                                         errors
                                       }
                                     }
                                     """;

    public const string UpdateEpic = """
                                     mutation UpdateEpic($input: WorkItemUpdateInput!) {
                                       workItemUpdate(input: $input) {
                                         workItem { id iid title state webUrl lockVersion }
                                         errors
                                       }
                                     }
                                     """;

    public const string DeleteEpic = """
                                     mutation DeleteEpic($input: WorkItemDeleteInput!) {
                                       workItemDelete(input: $input) {
                                         namespace { id fullPath }
                                         errors
                                       }
                                     }
                                     """;

    public const string AddNoteToEpic = """
                                        mutation AddNoteToEpic($input: CreateNoteInput!) {
                                          createNote(input: $input) {
                                            note { id body createdAt }
                                            errors
                                          }
                                        }
                                        """;

    public const string GetEpicChildren = """
                                          query GetEpicChildren($id: WorkItemID!, $first: Int!, $after: String) {
                                            workItem(id: $id) {
                                              id
                                              widgets {
                                                type
                                                ... on WorkItemWidgetHierarchy {
                                                  hasChildren
                                                  children(first: $first, after: $after) {
                                                    pageInfo { hasNextPage endCursor }
                                                    nodes { id iid title state webUrl workItemType { id name } }
                                                  }
                                                }
                                              }
                                            }
                                          }
                                          """;
}