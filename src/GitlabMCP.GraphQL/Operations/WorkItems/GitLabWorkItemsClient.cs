namespace GitlabMCP.GraphQL.Operations.WorkItems;

/// <summary>
///     Epic/work-item operations (DEC-011=C, DEC-020/021) — the surface <c>GitLab.Client</c> cannot provide
///     (no <c>IEpicsClient</c>/<c>IWorkItemsClient</c>). Every method applies the DEC-021 detection order:
///     top-level <c>errors</c> first (before even looking at <c>data</c>), then a mutation's own payload
///     <c>errors</c> field, then the null-with-zero-errors tier-gating ambiguity — never read a 200 status as
///     success by itself.
/// </summary>
public sealed class GitLabWorkItemsClient(IGitLabGraphQlClient client)
{
    private const int MaxPageSize = 100; // GitLab's hard server-side per-connection cap.

    public async Task<WorkItemNodeConnection> ListGroupEpicsAsync(
        string groupFullPath, int first, string? after, string? search, string? state,
        CancellationToken cancellationToken)
    {
        var clampedFirst = Math.Clamp(first, 1, MaxPageSize);
        var response = await client.ExecuteAsync<ListGroupEpicsVariables, ListGroupEpicsData>(
            new GraphQLRequest<ListGroupEpicsVariables>(
                WorkItemQueries.ListGroupEpics,
                new ListGroupEpicsVariables(groupFullPath, clampedFirst, after, search, state)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);

        return response.Data?.Group?.WorkItems
               ?? throw MakeUnavailable("epics",
                   $"Group '{groupFullPath}' was not found, is not visible to the configured token, or has no Epic feature (requires Premium or Ultimate).");
    }

    public async Task<WorkItemNode> GetEpicByIidAsync(string groupFullPath, string iid,
        CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<GetEpicByIidVariables, GetEpicByIidData>(
            new GraphQLRequest<GetEpicByIidVariables>(WorkItemQueries.GetEpicByIid,
                new GetEpicByIidVariables(groupFullPath, iid)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);

        return response.Data?.Group?.WorkItem
               ?? throw MakeUnavailable("epics",
                   $"Epic iid {iid} in group '{groupFullPath}' was not found, is not visible to the configured token, or this instance/namespace has no Epic feature (requires Premium or Ultimate).");
    }

    /// <summary>
    ///     DEC-011/021's own recommended pre-check: run before create, so a stale/wrong-namespace type id never reaches
    ///     <c>workItemCreate</c> as an ambiguous generic error.
    /// </summary>
    public async Task<string> ResolveEpicWorkItemTypeIdAsync(string groupFullPath, CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<GetEpicWorkItemTypeIdVariables, GetEpicWorkItemTypeIdData>(
            new GraphQLRequest<GetEpicWorkItemTypeIdVariables>(WorkItemQueries.GetEpicWorkItemTypeId,
                new GetEpicWorkItemTypeIdVariables(groupFullPath)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);

        var group = response.Data?.Group
                    ?? throw MakeUnavailable("epics",
                        $"Group '{groupFullPath}' was not found or is not visible to the configured token.");

        var typeNode = group.WorkItemTypes.Nodes.FirstOrDefault();
        if (typeNode is null || group.EpicsEnabled == false)
            throw MakeUnavailable("epics",
                $"Group '{groupFullPath}' does not have the Epic work item type available (requires Premium or Ultimate).");

        return typeNode.Id;
    }

    public async Task<WorkItemNode> CreateEpicAsync(WorkItemCreateInput input, CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<CreateEpicVariables, CreateEpicData>(
            new GraphQLRequest<CreateEpicVariables>(WorkItemQueries.CreateEpic, new CreateEpicVariables(input)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);
        var payload = response.Data?.WorkItemCreate ?? throw MakeServerError("workItemCreate returned no payload.");
        ThrowOnMutationErrors(payload.Errors);

        return payload.WorkItem ??
               throw MakeServerError("workItemCreate reported no errors but returned no work item.");
    }

    public async Task<WorkItemNode> UpdateEpicAsync(WorkItemUpdateInput input, CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<UpdateEpicVariables, UpdateEpicData>(
            new GraphQLRequest<UpdateEpicVariables>(WorkItemQueries.UpdateEpic, new UpdateEpicVariables(input)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);
        var payload = response.Data?.WorkItemUpdate ?? throw MakeServerError("workItemUpdate returned no payload.");
        ThrowOnMutationErrors(payload.Errors);

        return payload.WorkItem ??
               throw MakeServerError("workItemUpdate reported no errors but returned no work item.");
    }

    public async Task<string> DeleteEpicAsync(string workItemId, CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<DeleteEpicVariables, DeleteEpicData>(
            new GraphQLRequest<DeleteEpicVariables>(WorkItemQueries.DeleteEpic,
                new DeleteEpicVariables(new WorkItemDeleteInput(workItemId))),
            cancellationToken);

        ThrowOnTopLevelErrors(response);
        var payload = response.Data?.WorkItemDelete ?? throw MakeServerError("workItemDelete returned no payload.");
        ThrowOnMutationErrors(payload.Errors);

        return payload.Namespace?.FullPath ?? "(unknown namespace)";
    }

    public async Task<NoteNode> AddNoteToEpicAsync(string workItemId, string body, bool internalNote,
        CancellationToken cancellationToken)
    {
        var response = await client.ExecuteAsync<AddNoteToEpicVariables, AddNoteToEpicData>(
            new GraphQLRequest<AddNoteToEpicVariables>(WorkItemQueries.AddNoteToEpic,
                new AddNoteToEpicVariables(new CreateNoteInput(workItemId, body, internalNote))),
            cancellationToken);

        ThrowOnTopLevelErrors(response);
        var payload = response.Data?.CreateNote ?? throw MakeServerError("createNote returned no payload.");
        ThrowOnMutationErrors(payload.Errors);

        return payload.Note ?? throw MakeServerError("createNote reported no errors but returned no note.");
    }

    public async Task<WorkItemChildConnection> GetEpicChildrenAsync(string workItemId, int first, string? after,
        CancellationToken cancellationToken)
    {
        var clampedFirst = Math.Clamp(first, 1, MaxPageSize);
        var response = await client.ExecuteAsync<GetEpicChildrenVariables, GetEpicChildrenData>(
            new GraphQLRequest<GetEpicChildrenVariables>(WorkItemQueries.GetEpicChildren,
                new GetEpicChildrenVariables(workItemId, clampedFirst, after)),
            cancellationToken);

        ThrowOnTopLevelErrors(response);

        var hierarchy = response.Data?.WorkItem?.Widgets?.FirstOrDefault(static w => w.Type == "HIERARCHY");
        return hierarchy?.Children
               ?? throw MakeUnavailable("epics",
                   $"Work item {workItemId} was not found or is not visible to the configured token.");
    }

    private static void ThrowOnTopLevelErrors<TData>(GraphQLResponse<TData> response)
    {
        if (response.Errors is not { Count: > 0 } errors) return;

        throw new GitLabGraphQlValidationException(errors, "gitlab_graphql request was rejected.");
    }

    private static void ThrowOnMutationErrors(IReadOnlyList<string>? errors)
    {
        if (errors is { Count: > 0 })
            // Business-rule validation messages (bad title, invalid state transition, ...) are plain
            // strings GitLab itself generated for this exact request, not attacker-authored GitLab data
            // — safe to relay directly, unlike GraphQLError.Message (DEC-021).
            throw new GitLabGraphQlValidationException(
                [], $"gitlab_graphql mutation failed: {string.Join("; ", errors)}");
    }

    private static GitLabGraphQlUnavailableFeatureException MakeUnavailable(string featureName, string message)
    {
        return new GitLabGraphQlUnavailableFeatureException(featureName, message);
    }

    private static GitLabGraphQlServerException MakeServerError(string message)
    {
        return new GitLabGraphQlServerException(message);
    }
}