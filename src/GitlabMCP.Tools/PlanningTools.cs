using System.ComponentModel;
using System.Globalization;
using GitLab.Client.Abstractions;
using GitLab.Client.Abstractions.Exceptions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Planning;
using GitlabMCP.Mapping.Planning;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Planning-domain tools: issues, milestones, boards, labels, iterations, todos, resource groups and
///     resource-event history. Built directly on <c>GitLab.Client</c> (no GraphQL involved, unlike
///     <see cref="EpicTools" />). Every tool returning GitLab-authored text wraps via <see cref="GitLabContent" />.
/// </summary>
[McpServerToolType]
public sealed class PlanningTools(
    IIssuesClient issues,
    IMilestonesClient milestones,
    IBoardsClient boards,
    ILabelsClient labelsClient,
    IResourceGroupsClient resourceGroups,
    IIterationsClient iterations,
    ITodosClient todos,
    IResourceEventsClient resourceEvents,
    IResourceSubscriptionsClient subscriptions)
{
    private const int MaxLimit = 100;

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_issue_links
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_issue_links", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the issues linked to this one (relates_to / blocks / is_blocked_by). Each entry is the linked issue itself, carrying its link id and link type.")]
    public async Task<CallToolResult> ListIssueLinksAsync(
        [Description("Project: numeric id or \"namespace/path\", e.g. \"42\" or \"my-group/my-project\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI), not its global id.")]
        long issueIid,
        [Description("Maximum links to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<IssueLinkSummary> collected = [];
        var truncated = false;
        await foreach (var issue in issues.ListLinksAsync(project, issueIid, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToLinkSummary(issue));
        }

        return GitLabContent.Wrap(new IssueLinkListResult(collected, truncated), "projects/:id/issues/:iid/links");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_milestone_burndown_events
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_milestone_burndown_events", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists the burndown chart events of a milestone -- one entry per issue-weight change against it over time. GitLab Premium/Ultimate feature; expect a gitlab_forbidden error on lower plans.")]
    public async Task<CallToolResult> ListMilestoneBurndownEventsAsync(
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Maximum events to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var events = project is not null
            ? milestones.ListBurndownEventsAsync(project, milestoneId, cancellationToken)
            : milestones.ListBurndownEventsForGroupAsync(group!, milestoneId, cancellationToken);

        List<BurndownEventSummary> collected = [];
        var truncated = false;
        await foreach (var burndownEvent in events)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MilestoneMapper.ToSummary(burndownEvent));
        }

        return GitLabContent.Wrap(new BurndownEventListResult(collected, truncated),
            "projects|groups/:id/milestones/:id/burndown_events");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_board_lists
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_board_lists", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a board's lists (columns) in board order -- plain, label, milestone or iteration lists.")]
    public async Task<CallToolResult> ListBoardListsAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Maximum columns to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var lists = project is not null
            ? boards.ListListsForProjectAsync(project, boardId, cancellationToken)
            : boards.ListListsForGroupAsync(group!, boardId, cancellationToken);

        List<BoardColumnSummary> collected = [];
        var truncated = false;
        await foreach (var list in lists)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(BoardMapper.ToColumnSummary(list));
        }

        return GitLabContent.Wrap(new BoardColumnListResult(collected, truncated),
            "projects|groups/:id/boards/:id/lists");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_labels
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_labels", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a project's or group's labels, optionally with open/closed issue and merge-request counts.")]
    public async Task<CallToolResult> ListLabelsAsync(
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Optional free-text search over label names.")]
        string? search = null,
        [Description("If true, also include labels inherited from ancestor groups. Default false.")]
        bool includeAncestorGroups = false,
        [Description(
            "If true, include each label's open/closed issue and merge-request counts (slower to compute). Default false.")]
        bool withCounts = false,
        [Description("Maximum labels to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        List<LabelSummary> collected = [];
        var truncated = false;
        var perPage = Math.Min(limit + 1, MaxLimit);

        if (project is not null)
        {
            var options = new LabelListOptions
            {
                Search = search,
                IncludeAncestorGroups = includeAncestorGroups,
                WithCounts = withCounts,
                PerPage = perPage
            };

            await foreach (var label in labelsClient.ListAsync(project, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(LabelMapper.ToSummary(label));
            }
        }
        else
        {
            var options = new GroupLabelListOptions
            {
                Search = search,
                IncludeAncestorGroups = includeAncestorGroups,
                WithCounts = withCounts,
                PerPage = perPage
            };

            await foreach (var label in labelsClient.ListForGroupAsync(group!, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(LabelMapper.ToSummary(label));
            }
        }

        return GitLabContent.Wrap(new LabelListResult(collected, truncated), "projects|groups/:id/labels");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_resource_groups
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_resource_groups", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the CI/CD resource groups (deployment-concurrency mutexes) a project's pipelines have created.")]
    public async Task<CallToolResult> ListResourceGroupsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum resource groups to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<ResourceGroupSummary> collected = [];
        var truncated = false;
        await foreach (var resourceGroup in resourceGroups.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ResourceGroupMapper.ToSummary(resourceGroup));
        }

        return GitLabContent.Wrap(new ResourceGroupListResult(collected, truncated), "projects/:id/resource_groups");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_iterations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_iterations", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the iterations visible to a group or project. Read-only: iterations are scheduled through cadences this library doesn't expose, and a project's iterations actually come from its ancestor groups.")]
    public async Task<CallToolResult> ListIterationsAsync(
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Optional free-text search over iteration titles.")]
        string? search = null,
        [Description(
            "Optional state filter: \"opened\", \"upcoming\", \"current\", \"closed\", or \"all\". Omit for all.")]
        string? state = null,
        [Description("Maximum iterations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var options = new IterationListOptions
        {
            Search = search,
            State = ParseIterationState(state),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        var iterationStream = project is not null
            ? iterations.ListForProjectAsync(project, options, cancellationToken)
            : iterations.ListForGroupAsync(group!, options, cancellationToken);

        List<IterationSummary> collected = [];
        var truncated = false;
        await foreach (var iteration in iterationStream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IterationMapper.ToSummary(iteration));
        }

        return GitLabContent.Wrap(new IterationListResult(collected, truncated), "projects|groups/:id/iterations");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_todos
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_todos", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the authenticated user's to-do items, filterable by action, author, project, group, state, or type. With no filters, GitLab returns only pending items.")]
    public async Task<CallToolResult> ListTodosAsync(
        [Description(
            "Optional action filter, e.g. \"assigned\", \"mentioned\", \"review_requested\", \"build_failed\", \"marked\", \"approval_required\", \"unmergeable\", \"directly_addressed\". Omit for all actions.")]
        string? action = null,
        [Description("Optional state filter: \"pending\" or \"done\". Omit for pending only.")]
        string? state = null,
        [Description(
            "Optional target type filter, e.g. \"Issue\", \"MergeRequest\", \"Epic\", \"DesignManagement::Design\", \"AlertManagement::Alert\". Omit for all types.")]
        string? type = null,
        [Description("Optional numeric id of the user who triggered the to-do.")]
        long? authorId = null,
        [Description("Optional numeric project id to filter to.")]
        long? projectId = null,
        [Description("Optional numeric group id to filter to.")]
        long? groupId = null,
        [Description("Maximum to-dos to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new TodoListOptions
        {
            Action = action,
            State = ParseTodoState(state),
            Type = type,
            AuthorId = authorId,
            ProjectId = projectId,
            GroupId = groupId,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<TodoSummary> collected = [];
        var truncated = false;
        await foreach (var todo in todos.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(TodoMapper.ToSummary(todo));
        }

        return GitLabContent.Wrap(new TodoListResult(collected, truncated), "todos");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_resource_events
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_resource_events", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the machine-readable change history of an issue, merge request or epic: who changed its label/state/milestone/iteration/weight and when. Not every family exists for every eventable type -- issues have all five, merge requests have label/state/milestone only, epics have label/state only.")]
    public async Task<CallToolResult> ListResourceEventsAsync(
        [Description("Which kind of item to read history for: \"issue\", \"merge_request\", or \"epic\".")]
        string eventableType,
        [Description(
            "Which change family to list: \"label\", \"state\", \"milestone\", \"iteration\", or \"weight\". Availability depends on eventableType -- issues support all five, merge requests support label/state/milestone, epics support label/state.")]
        string eventFamily,
        [Description("The issue's, merge request's, or epic's iid (the number shown in the GitLab UI).")]
        long iid,
        [Description(
            "Project: numeric id or \"namespace/path\". Required when eventableType is \"issue\" or \"merge_request\".")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Required when eventableType is \"epic\".")]
        string? group = null,
        [Description("Maximum events to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var type = eventableType.ToLowerInvariant();
        var family = eventFamily.ToLowerInvariant();

        var comboValid = (type, family) switch
        {
            ("issue", "label" or "state" or "milestone" or "iteration" or "weight") => true,
            ("merge_request", "label" or "state" or "milestone") => true,
            ("epic", "label" or "state") => true,
            _ => false
        };

        if (!comboValid)
            throw new McpException(
                "Invalid eventableType/eventFamily combination. eventableType must be \"issue\", " +
                "\"merge_request\", or \"epic\"; eventFamily must be one of \"label\"/\"state\"/\"milestone\"/" +
                "\"iteration\"/\"weight\" for issues, \"label\"/\"state\"/\"milestone\" for merge_request, " +
                "or \"label\"/\"state\" for epic.");

        if (type == "epic")
        {
            if (string.IsNullOrWhiteSpace(group))
                throw new McpException("group is required when eventableType is \"epic\".");
        }
        else if (string.IsNullOrWhiteSpace(project))
        {
            throw new McpException("project is required when eventableType is \"issue\" or \"merge_request\".");
        }

        var options = new ResourceEventListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        var (events, truncated) = (type, family) switch
        {
            ("issue", "label") => await CollectAsync(
                resourceEvents.ListIssueLabelEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromLabelEvent, limit),
            ("issue", "state") => await CollectAsync(
                resourceEvents.ListIssueStateEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromStateEvent, limit),
            ("issue", "milestone") => await CollectAsync(
                resourceEvents.ListIssueMilestoneEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromMilestoneEvent, limit),
            ("issue", "iteration") => await CollectAsync(
                resourceEvents.ListIssueIterationEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromIterationEvent, limit),
            ("issue", "weight") => await CollectAsync(
                resourceEvents.ListIssueWeightEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromWeightEvent, limit),
            ("merge_request", "label") => await CollectAsync(
                resourceEvents.ListMergeRequestLabelEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromLabelEvent, limit),
            ("merge_request", "state") => await CollectAsync(
                resourceEvents.ListMergeRequestStateEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromStateEvent, limit),
            ("merge_request", "milestone") => await CollectAsync(
                resourceEvents.ListMergeRequestMilestoneEventsAsync(project!, iid, options, cancellationToken),
                ResourceEventMapper.FromMilestoneEvent, limit),
            ("epic", "label") => await CollectAsync(
                resourceEvents.ListEpicLabelEventsAsync(group!, iid, options, cancellationToken),
                ResourceEventMapper.FromLabelEvent, limit),
            ("epic", "state") => await CollectAsync(
                resourceEvents.ListEpicStateEventsAsync(group!, iid, options, cancellationToken),
                ResourceEventMapper.FromStateEvent, limit),
            _ => throw new McpException("Unreachable: the eventableType/eventFamily combination was already validated.")
        };

        return GitLabContent.Wrap(new ResourceEventListResult(events, truncated),
            "issues|merge_requests/:iid|epics/:iid/resource_*_events");
    }

    private static async Task<(List<ResourceEventSummary> Events, bool Truncated)> CollectAsync<TSource>(
        IAsyncEnumerable<TSource> source, Func<TSource, ResourceEventSummary> map, int limit)
    {
        List<ResourceEventSummary> items = [];
        var truncated = false;
        await foreach (var item in source)
        {
            if (items.Count == limit)
            {
                truncated = true;
                break;
            }

            items.Add(map(item));
        }

        return (items, truncated);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_issue_participants
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_issue_participants", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the users participating in an issue's discussion (note authors, assignees, and anyone else GitLab counts as a participant).")]
    public async Task<CallToolResult> ListIssueParticipantsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description("Maximum participants to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<ParticipantSummary> collected = [];
        var truncated = false;
        await foreach (var user in issues.ListParticipantsAsync(project, issueIid, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToParticipantSummary(user));
        }

        return GitLabContent.Wrap(new ParticipantListResult(collected, truncated),
            "projects/:id/issues/:iid/participants");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_milestone_issues
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_milestone_issues", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists every issue assigned to a milestone.")]
    public async Task<CallToolResult> ListMilestoneIssuesAsync(
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Maximum issues to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var issueStream = project is not null
            ? milestones.ListIssuesAsync(project, milestoneId, cancellationToken)
            : milestones.ListIssuesForGroupAsync(group!, milestoneId, cancellationToken);

        List<IssueSummary> collected = [];
        var truncated = false;
        await foreach (var issue in issueStream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToSummary(issue));
        }

        return GitLabContent.Wrap(new IssueListResult(collected, truncated),
            "projects|groups/:id/milestones/:id/issues");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_boards
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_boards", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a project's or group's issue boards.")]
    public async Task<CallToolResult> ListBoardsAsync(
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Maximum boards to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var boardStream = project is not null
            ? boards.ListForProjectAsync(project, cancellationToken)
            : boards.ListForGroupAsync(group!, cancellationToken);

        List<BoardSummary> collected = [];
        var truncated = false;
        await foreach (var board in boardStream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(BoardMapper.ToSummary(board));
        }

        return GitLabContent.Wrap(new BoardSummaryListResult(collected, truncated), "projects|groups/:id/boards");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_label
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_label", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one label by its exact name at project or group scope.")]
    public async Task<CallToolResult> GetLabelAsync(
        [Description("The label's exact name (case-sensitive).")]
        string name,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("If true, also look at labels inherited from ancestor groups. Default false.")]
        bool includeAncestorGroups = false,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        LabelSummary summary;
        if (project is not null)
        {
            var options = new LabelGetOptions { IncludeAncestorGroups = includeAncestorGroups };
            var label = await labelsClient.GetAsync(project, name, options, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }
        else
        {
            var options = new GroupLabelGetOptions { IncludeAncestorGroups = includeAncestorGroups };
            var label = await labelsClient.GetForGroupAsync(group!, name, options, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }

        return GitLabContent.Wrap(summary, "projects|groups/:id/labels/:name");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_issue", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new issue in a GitLab project.")]
    public async Task<CallToolResult> CreateIssueAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's title.")] string title,
        [Description("Optional description in GitLab-flavored Markdown.")]
        string? description = null,
        [Description("Optional comma-separated label names to apply, e.g. \"bug,urgent\".")]
        string? labels = null,
        [Description("Optional comma-separated numeric user ids to assign.")]
        string? assigneeIds = null,
        [Description("Optional numeric milestone id to assign.")]
        long? milestoneId = null,
        [Description("Optional due date, ISO 8601 (YYYY-MM-DD).")]
        string? dueDate = null,
        [Description("Optional start date, ISO 8601 (YYYY-MM-DD).")]
        string? startDate = null,
        [Description("Optional non-negative weight, used for prioritization.")]
        int? weight = null,
        [Description("If true, the issue is only visible to project members. Default false.")]
        bool confidential = false,
        [Description(
            "Optional issue type: \"issue\", \"incident\", \"test_case\", \"requirement\", \"task\", or \"ticket\". Defaults to \"issue\" when omitted.")]
        string? issueType = null,
        [Description("Optional severity for incidents: \"unknown\", \"low\", \"medium\", \"high\", or \"critical\".")]
        string? severity = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateIssueRequest
        {
            Title = title,
            Description = description,
            Labels = SplitList(labels),
            AssigneeIds = SplitLongList(assigneeIds, nameof(assigneeIds)),
            MilestoneId = milestoneId,
            DueDate = ParseDate(dueDate, nameof(dueDate)),
            StartDate = ParseDate(startDate, nameof(startDate)),
            Weight = weight,
            Confidential = confidential,
            IssueType = ParseIssueType(issueType),
            Severity = ParseIssueSeverity(severity)
        };

        var issue = await issues.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_board
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_board", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new issue board on a project or group.")]
    public async Task<CallToolResult> CreateBoardAsync(
        [Description("The new board's name.")] string name,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var request = new CreateBoardRequest { Name = name };
        var board = project is not null
            ? await boards.CreateForProjectAsync(project, request, cancellationToken)
            : await boards.CreateForGroupAsync(group!, request, cancellationToken);

        return GitLabContent.Wrap(BoardMapper.ToSummary(board), "projects|groups/:id/boards (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_milestone
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_milestone", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a milestone with a title, description, and start/due dates at project or group scope.")]
    public async Task<CallToolResult> CreateMilestoneAsync(
        [Description("The milestone's title.")]
        string title,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Optional description in GitLab-flavored Markdown.")]
        string? description = null,
        [Description("Optional start date, ISO 8601 (YYYY-MM-DD).")]
        string? startDate = null,
        [Description("Optional due date, ISO 8601 (YYYY-MM-DD).")]
        string? dueDate = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var request = new CreateMilestoneRequest
        {
            Title = title,
            Description = description,
            StartDate = ParseDate(startDate, nameof(startDate)),
            DueDate = ParseDate(dueDate, nameof(dueDate))
        };

        var milestone = project is not null
            ? await milestones.CreateAsync(project, request, cancellationToken)
            : await milestones.CreateForGroupAsync(group!, request, cancellationToken);

        return GitLabContent.Wrap(MilestoneMapper.ToSummary(milestone), "projects|groups/:id/milestones (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_label
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_label", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a label with a name and color (and, at project scope, a priority) at project or group scope.")]
    public async Task<CallToolResult> CreateLabelAsync(
        [Description("The label's name.")] string name,
        [Description("The label's color: a hex code such as \"#FF0000\", or one of GitLab's named CSS colors.")]
        string color,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Optional description.")] string? description = null,
        [Description(
            "Optional priority (a lower number sorts first on the issue board). Project-scoped labels only -- omit when creating a group label.")]
        int? priority = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (group is not null && priority is not null)
            throw new McpException(
                "priority is only supported for a project-scoped label; omit it when creating a group label.");

        LabelSummary summary;
        if (project is not null)
        {
            var request = new CreateLabelRequest
                { Name = name, Color = color, Description = description, Priority = priority };
            var label = await labelsClient.CreateAsync(project, request, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }
        else
        {
            var request = new CreateGroupLabelRequest { Name = name, Color = color, Description = description };
            var label = await labelsClient.CreateForGroupAsync(group!, request, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }

        return GitLabContent.Wrap(summary, "projects|groups/:id/labels (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_todo
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_todo", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a to-do item for yourself on an issue or merge request.")]
    public async Task<CallToolResult> CreateTodoAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Which kind of item this to-do is for: \"issue\" or \"merge_request\".")]
        string targetType,
        [Description("The issue's or merge request's iid (the number shown in the GitLab UI).")]
        long targetIid,
        CancellationToken cancellationToken = default)
    {
        var todo = targetType.ToLowerInvariant() switch
        {
            "issue" => await todos.CreateForIssueAsync(project, targetIid, cancellationToken),
            "merge_request" => await todos.CreateForMergeRequestAsync(project, targetIid, cancellationToken),
            _ => throw new McpException("targetType must be \"issue\" or \"merge_request\".")
        };

        return GitLabContent.Wrap(TodoMapper.ToSummary(todo), "projects/:id/issues|merge_requests/:iid/todo (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_issue_subscription
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_issue_subscription", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Subscribes or unsubscribes the authenticated user to an issue's notifications.")]
    public async Task<CallToolResult> SetIssueSubscriptionAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description("True to subscribe, false to unsubscribe.")]
        bool subscribe,
        CancellationToken cancellationToken = default)
    {
        var issue = subscribe
            ? await subscriptions.SubscribeToIssueAsync(project, issueIid, cancellationToken)
            : await subscriptions.UnsubscribeFromIssueAsync(project, issueIid, cancellationToken);

        return GitLabContent.Wrap(IssueMapper.ToSubscriptionResult(issue), "projects/:id/issues/:iid/subscribe");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_resource_group_process_mode
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_resource_group_process_mode", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Changes the order in which a resource group releases its queued CI/CD jobs.")]
    public async Task<CallToolResult> SetResourceGroupProcessModeAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The resource group's key, as set by the \"resource_group\" keyword in .gitlab-ci.yml.")]
        string key,
        [Description("The new process mode: \"unordered\", \"oldest_first\", or \"newest_first\".")]
        string processMode,
        CancellationToken cancellationToken = default)
    {
        var mode = processMode.ToLowerInvariant() switch
        {
            "unordered" => "unordered",
            "oldest_first" => "oldest_first",
            "newest_first" => "newest_first",
            _ => throw new McpException("processMode must be \"unordered\", \"oldest_first\", or \"newest_first\".")
        };

        var request = new UpdateResourceGroupRequest { ProcessMode = mode };
        var resourceGroup = await resourceGroups.UpdateAsync(project, key, request, cancellationToken);

        return GitLabContent.Wrap(ResourceGroupMapper.ToSummary(resourceGroup),
            "projects/:id/resource_groups/:key (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_issue", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Applies a partial update to an issue's fields (title, description, labels, assignees, milestone, weight, severity, confidentiality, dates, type) without touching its open/closed state. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateIssueAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description("New title, or omitted to leave unchanged.")]
        string? title = null,
        [Description("New description, or omitted to leave unchanged.")]
        string? description = null,
        [Description(
            "Replaces the issue's entire label set with this comma-separated list of names, or omitted to leave unchanged. Prefer addLabels/removeLabels to change only some labels.")]
        string? labels = null,
        [Description("Optional comma-separated label names to add, leaving existing labels alone.")]
        string? addLabels = null,
        [Description("Optional comma-separated label names to remove, leaving the rest alone.")]
        string? removeLabels = null,
        [Description(
            "Replaces the issue's assignees with this comma-separated list of numeric user ids, or omitted to leave unchanged.")]
        string? assigneeIds = null,
        [Description("New numeric milestone id, or omitted to leave unchanged.")]
        long? milestoneId = null,
        [Description("New due date, ISO 8601 (YYYY-MM-DD), or omitted to leave unchanged.")]
        string? dueDate = null,
        [Description("New start date, ISO 8601 (YYYY-MM-DD), or omitted to leave unchanged.")]
        string? startDate = null,
        [Description("New weight, or omitted to leave unchanged.")]
        int? weight = null,
        [Description(
            "New severity: \"unknown\", \"low\", \"medium\", \"high\", or \"critical\", or omitted to leave unchanged.")]
        string? severity = null,
        [Description(
            "New issue type: \"issue\", \"incident\", \"test_case\", \"requirement\", \"task\", or \"ticket\", or omitted to leave unchanged.")]
        string? issueType = null,
        [Description("New confidentiality flag, or omitted to leave unchanged.")]
        bool? confidential = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateIssueRequest
        {
            Title = title,
            Description = description,
            Labels = SplitList(labels),
            AddLabels = SplitList(addLabels),
            RemoveLabels = SplitList(removeLabels),
            AssigneeIds = SplitLongList(assigneeIds, nameof(assigneeIds)),
            MilestoneId = milestoneId,
            DueDate = ParseDate(dueDate, nameof(dueDate)),
            StartDate = ParseDate(startDate, nameof(startDate)),
            Weight = weight,
            Severity = ParseIssueSeverity(severity),
            IssueType = ParseIssueType(issueType),
            Confidential = confidential
        };

        var issue = await issues.UpdateAsync(project, issueIid, request, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_issue", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one issue by project and its project-scoped iid (the number shown in the GitLab UI).")]
    public async Task<CallToolResult> GetIssueAsync(
        [Description("Project: numeric id or \"namespace/path\", e.g. \"42\" or \"my-group/my-project\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI), not its global id.")]
        long issueIid,
        CancellationToken cancellationToken = default)
    {
        var issue = await issues.GetAsync(project, issueIid, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_issue_by_global_id
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_issue_by_global_id", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one issue by its instance-wide global id (as seen in webhooks, search results, or cross-project references) rather than its project-scoped iid.")]
    public async Task<CallToolResult> GetIssueByGlobalIdAsync(
        [Description("The issue's global id -- an instance-wide number, distinct from its project-scoped iid.")]
        long issueId,
        CancellationToken cancellationToken = default)
    {
        var issue = await issues.GetByIdAsync(issueId, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "issues/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_issues
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_issues", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists and filters a project's issues, newest-updated first. Returns a compact summary per issue; call gitlab_get_issue for the full description of one.")]
    public async Task<CallToolResult> ListIssuesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Filter by state: \"opened\", \"closed\", \"all\", or omit for all.")]
        string? state = null,
        [Description("Optional comma-separated label names; a returned issue carries every one of them.")]
        string? labels = null,
        [Description("Optional exact milestone title to filter by.")]
        string? milestone = null,
        [Description("Optional exact assignee username to filter by.")]
        string? assigneeUsername = null,
        [Description("Optional exact weight to filter by.")]
        int? weight = null,
        [Description("Optional free-text search over title and description.")]
        string? search = null,
        [Description("Optional ISO 8601 timestamp; only issues created at or after this time are returned.")]
        string? createdAfter = null,
        [Description("Optional ISO 8601 timestamp; only issues created at or before this time are returned.")]
        string? createdBefore = null,
        [Description("Maximum issues to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new IssueListOptions
        {
            State = ParseIssueState(state),
            Labels = SplitList(labels),
            Milestone = string.IsNullOrWhiteSpace(milestone) ? null : milestone,
            AssigneeUsername = string.IsNullOrWhiteSpace(assigneeUsername) ? null : [assigneeUsername],
            Weight = weight?.ToString(CultureInfo.InvariantCulture),
            Search = search,
            CreatedAfter = ParseTimestamp(createdAfter, nameof(createdAfter)),
            CreatedBefore = ParseTimestamp(createdBefore, nameof(createdBefore)),
            OrderBy = GitLabIssueOrderBy.UpdatedAt,
            Sort = GitLabIssueSort.Desc,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<IssueSummary> collected = [];
        var truncated = false;
        await foreach (var issue in issues.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToSummary(issue));
        }

        return GitLabContent.Wrap(new IssueListResult(collected, truncated), "projects/:id/issues");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_my_issues
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_my_issues", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists issues visible to the authenticated user across every project. Defaults to issues the user authored; widen with scope to also see issues assigned to them or every issue GitLab associates with them.")]
    public async Task<CallToolResult> ListMyIssuesAsync(
        [Description("Scope: \"authored\" (default when omitted), \"assigned_to_me\", or \"all\".")]
        string? scope = null,
        [Description("Filter by state: \"opened\", \"closed\", \"all\", or omit for all.")]
        string? state = null,
        [Description("Maximum issues to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new IssueListOptions
        {
            Scope = ParseIssueScope(scope),
            State = ParseIssueState(state),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<IssueSummary> collected = [];
        var truncated = false;
        await foreach (var issue in issues.ListForCurrentUserAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToSummary(issue));
        }

        return GitLabContent.Wrap(new IssueListResult(collected, truncated), "issues");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_close_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_close_issue", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Closes an issue.")]
    public async Task<CallToolResult> CloseIssueAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        CancellationToken cancellationToken = default)
    {
        var issue = await issues.CloseAsync(project, issueIid, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid (close)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_reopen_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_reopen_issue", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Reopens a closed issue.")]
    public async Task<CallToolResult> ReopenIssueAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        CancellationToken cancellationToken = default)
    {
        var issue = await issues.ReopenAsync(project, issueIid, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid (reopen)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_issue", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes an issue. Requires owner or administrator rights on the token. Prefer gitlab_close_issue for ordinary workflow -- this cannot be undone.")]
    public async Task<IssueDeleteResult> DeleteIssueAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        CancellationToken cancellationToken = default)
    {
        await issues.DeleteAsync(project, issueIid, cancellationToken);
        return new IssueDeleteResult(issueIid, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_move_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_move_issue", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Moves an issue to a different project. The issue is relocated under a new iid in the target project and the original is redirected there, not copied. This cannot be undone.")]
    public async Task<CallToolResult> MoveIssueAsync(
        [Description("Project the issue currently belongs to: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI) in its current project.")]
        long issueIid,
        [Description("The numeric id of the project to move the issue into.")]
        long toProjectId,
        CancellationToken cancellationToken = default)
    {
        var request = new MoveIssueRequest { ToProjectId = toProjectId };
        var issue = await issues.MoveAsync(project, issueIid, request, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid/move");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_clone_issue
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_clone_issue", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Copies an issue into another project, optionally including its notes, leaving the original issue in place. Returns the new copy.")]
    public async Task<CallToolResult> CloneIssueAsync(
        [Description("Project the issue currently belongs to: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI) to copy.")]
        long issueIid,
        [Description("The numeric id of the project to copy the issue into.")]
        long toProjectId,
        [Description("If true, also copy the issue's notes/comments into the new copy. Default false.")]
        bool withNotes = false,
        CancellationToken cancellationToken = default)
    {
        var request = new CloneIssueRequest { ToProjectId = toProjectId, WithNotes = withNotes };
        var issue = await issues.CloneAsync(project, issueIid, request, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToSummary(issue), "projects/:id/issues/:iid/clone");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_link_issues
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_link_issues", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a relation between this issue and another, possibly in a different project: relates_to, blocks, or is_blocked_by.")]
    public async Task<CallToolResult> LinkIssuesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The source issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description("The target issue's project: numeric id or \"namespace/path\".")]
        string targetProject,
        [Description("The target issue's iid (the number shown in the GitLab UI).")]
        long targetIssueIid,
        [Description("The relation type: \"relates_to\" (default when omitted), \"blocks\", or \"is_blocked_by\".")]
        string? linkType = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateIssueLinkRequest
        {
            TargetProjectId = targetProject,
            TargetIssueIid = targetIssueIid,
            LinkType = ParseIssueLinkType(linkType)
        };

        var link = await issues.CreateLinkAsync(project, issueIid, request, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToLinkResult(link), "projects/:id/issues/:iid/links (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_unlink_issues
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_unlink_issues", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes a relation between two issues by its link id (from gitlab_link_issues or gitlab_list_issue_links).")]
    public async Task<IssueUnlinkResult> UnlinkIssuesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The iid (the number shown in the GitLab UI) of the issue that owns the link.")]
        long issueIid,
        [Description("The link's id, from gitlab_link_issues or gitlab_list_issue_links.")]
        long issueLinkId,
        CancellationToken cancellationToken = default)
    {
        await issues.DeleteLinkAsync(project, issueIid, issueLinkId, cancellationToken);
        return new IssueUnlinkResult(issueLinkId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_issue_time_stats
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_issue_time_stats", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the time-tracking totals (estimate and time spent, human-readable and in raw seconds) for an issue.")]
    public async Task<CallToolResult> GetIssueTimeStatsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        CancellationToken cancellationToken = default)
    {
        var stats = await issues.GetTimeStatsAsync(project, issueIid, cancellationToken);
        return GitLabContent.Wrap(IssueMapper.ToTimeStats(stats), "projects/:id/issues/:iid/time_stats");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_manage_issue_time
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_manage_issue_time", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adjusts an issue's time tracking in one action-based tool: sets or clears the time estimate, or logs or clears spent time. Clearing discards recorded values irreversibly.")]
    public async Task<CallToolResult> ManageIssueTimeAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description(
            "The action to perform: \"set_estimate\", \"reset_estimate\", \"add_spent_time\", or \"reset_spent_time\".")]
        string action,
        [Description(
            "GitLab human-readable duration, e.g. \"3h30m\" or \"1d\". Required for \"set_estimate\" and \"add_spent_time\"; ignored otherwise.")]
        string? duration = null,
        CancellationToken cancellationToken = default)
    {
        var stats = action.ToLowerInvariant() switch
        {
            "set_estimate" => await issues.SetTimeEstimateAsync(
                project, issueIid,
                new SetIssueTimeEstimateRequest { Duration = RequireDuration(duration, "set_estimate") },
                cancellationToken),
            "reset_estimate" => await issues.ResetTimeEstimateAsync(project, issueIid, cancellationToken),
            "add_spent_time" => await issues.AddSpentTimeAsync(
                project, issueIid,
                new AddIssueSpentTimeRequest { Duration = RequireDuration(duration, "add_spent_time") },
                cancellationToken),
            "reset_spent_time" => await issues.ResetSpentTimeAsync(project, issueIid, cancellationToken),
            _ => throw new McpException(
                "action must be \"set_estimate\", \"reset_estimate\", \"add_spent_time\", or \"reset_spent_time\".")
        };

        return GitLabContent.Wrap(IssueMapper.ToTimeStats(stats),
            "projects/:id/issues/:iid/time_estimate|reset_time_estimate|add_spent_time|reset_spent_time");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_issue_statistics
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_issue_statistics", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Counts issues by state (opened/closed/all) at instance, group, or project scope, using the same filters as gitlab_list_issues. Provide project or group to narrow the scope; provide neither for an instance-wide count of issues visible to the token.")]
    public async Task<IssueStatisticsResult> GetIssueStatisticsAsync(
        [Description(
            "Project: numeric id or \"namespace/path\". Specify this, group, or neither (for an instance-wide count); never both project and group.")]
        string? project = null,
        [Description(
            "Group: numeric id or \"namespace/path\". Specify this, project, or neither (for an instance-wide count); never both project and group.")]
        string? group = null,
        [Description("Optional comma-separated label names; a counted issue carries every one of them.")]
        string? labels = null,
        [Description("Optional exact milestone title to filter by.")]
        string? milestone = null,
        [Description("Optional free-text search over title and description.")]
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (project is not null && group is not null)
            throw new McpException("Specify at most one of project or group, not both.");

        var options = new IssueStatisticsOptions
        {
            Labels = SplitList(labels),
            Milestone = string.IsNullOrWhiteSpace(milestone) ? null : milestone,
            Search = search
        };

        var statistics = project is not null
            ? await issues.GetProjectStatisticsAsync(project, options, cancellationToken)
            : group is not null
                ? await issues.GetGroupStatisticsAsync(group, options, cancellationToken)
                : await issues.GetStatisticsAsync(options, cancellationToken);

        return IssueMapper.ToStatisticsResult(statistics);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_issue_related_merge_requests
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_issue_related_merge_requests", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists merge requests that mention this issue. Set closingOnly to true to see only the ones that will close it on merge.")]
    public async Task<CallToolResult> ListIssueRelatedMergeRequestsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The issue's iid (the number shown in the GitLab UI).")]
        long issueIid,
        [Description(
            "If true, list only merge requests that will close this issue on merge, instead of every merge request that mentions it. Default false.")]
        bool closingOnly = false,
        [Description("Maximum merge requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var mergeRequests = closingOnly
            ? issues.ListClosedByAsync(project, issueIid, cancellationToken)
            : issues.ListRelatedMergeRequestsAsync(project, issueIid, cancellationToken);

        List<RelatedMergeRequestSummary> collected = [];
        var truncated = false;
        await foreach (var mergeRequest in mergeRequests)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToRelatedMergeRequestSummary(mergeRequest));
        }

        return GitLabContent.Wrap(
            new RelatedMergeRequestListResult(collected, truncated),
            "projects/:id/issues/:iid/related_merge_requests|closed_by");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_milestones
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_milestones", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a project's or group's milestones, filterable by state, title, or search text.")]
    public async Task<CallToolResult> ListMilestonesAsync(
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Optional state filter: \"active\" or \"closed\". Omit for all.")]
        string? state = null,
        [Description("Optional exact milestone title to filter by.")]
        string? title = null,
        [Description("Optional free-text search over milestone titles and descriptions.")]
        string? search = null,
        [Description("Maximum milestones to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        List<MilestoneSummary> collected = [];
        var truncated = false;
        var perPage = Math.Min(limit + 1, MaxLimit);
        var parsedState = ParseMilestoneState(state);
        var trimmedTitle = string.IsNullOrWhiteSpace(title) ? null : title;

        if (project is not null)
        {
            var options = new MilestoneListOptions
            {
                State = parsedState,
                Title = trimmedTitle,
                Search = search,
                PerPage = perPage
            };

            await foreach (var milestone in milestones.ListAsync(project, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(MilestoneMapper.ToSummary(milestone));
            }
        }
        else
        {
            var options = new GroupMilestoneListOptions
            {
                State = parsedState,
                Title = trimmedTitle,
                Search = search,
                PerPage = perPage
            };

            await foreach (var milestone in milestones.ListForGroupAsync(group!, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(MilestoneMapper.ToSummary(milestone));
            }
        }

        return GitLabContent.Wrap(new MilestoneListResult(collected, truncated), "projects|groups/:id/milestones");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_milestone
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_milestone", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one milestone by its numeric id (not its iid) at project or group scope.")]
    public async Task<CallToolResult> GetMilestoneAsync(
        [Description("The milestone's numeric id (not its iid). Use gitlab_list_milestones to find it.")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var milestone = project is not null
            ? await milestones.GetAsync(project, milestoneId, cancellationToken)
            : await milestones.GetForGroupAsync(group!, milestoneId, cancellationToken);

        return GitLabContent.Wrap(MilestoneMapper.ToSummary(milestone), "projects|groups/:id/milestones/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_milestone
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_milestone", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a milestone's title, description, or dates, or closes/reactivates it. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateMilestoneAsync(
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("New title, or omitted to leave unchanged.")]
        string? title = null,
        [Description("New description, or omitted to leave unchanged.")]
        string? description = null,
        [Description("New start date, ISO 8601 (YYYY-MM-DD), or omitted to leave unchanged.")]
        string? startDate = null,
        [Description("New due date, ISO 8601 (YYYY-MM-DD), or omitted to leave unchanged.")]
        string? dueDate = null,
        [Description("\"close\" or \"activate\" to change state, or omitted to leave unchanged.")]
        string? stateEvent = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var request = new UpdateMilestoneRequest
        {
            Title = title,
            Description = description,
            StartDate = ParseDate(startDate, nameof(startDate)),
            DueDate = ParseDate(dueDate, nameof(dueDate)),
            StateEvent = ParseMilestoneStateEvent(stateEvent)
        };

        var milestone = project is not null
            ? await milestones.UpdateAsync(project, milestoneId, request, cancellationToken)
            : await milestones.UpdateForGroupAsync(group!, milestoneId, request, cancellationToken);

        return GitLabContent.Wrap(MilestoneMapper.ToSummary(milestone), "projects|groups/:id/milestones/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_milestone
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_milestone", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a milestone at project or group scope. This cannot be undone.")]
    public async Task<MilestoneDeleteResult> DeleteMilestoneAsync(
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (project is not null)
            await milestones.DeleteAsync(project, milestoneId, cancellationToken);
        else
            await milestones.DeleteForGroupAsync(group!, milestoneId, cancellationToken);

        return new MilestoneDeleteResult(milestoneId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_promote_milestone
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_promote_milestone", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Promotes a project milestone to group scope, moving every issue and merge request assignment with it. Irreversible. Project-scoped milestones only -- a group milestone is already at the top of the hierarchy and cannot be promoted further.")]
    public async Task<MilestonePromoteResult> PromoteMilestoneAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        CancellationToken cancellationToken = default)
    {
        await milestones.PromoteAsync(project, milestoneId, cancellationToken);
        return new MilestonePromoteResult(milestoneId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_milestone_merge_requests
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_milestone_merge_requests", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Lists every merge request assigned to a milestone.")]
    public async Task<CallToolResult> ListMilestoneMergeRequestsAsync(
        [Description("The milestone's numeric id (not its iid).")]
        long milestoneId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("Maximum merge requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        RequireExactlyOneScope(project, group);

        var mergeRequestStream = project is not null
            ? milestones.ListMergeRequestsAsync(project, milestoneId, cancellationToken)
            : milestones.ListMergeRequestsForGroupAsync(group!, milestoneId, cancellationToken);

        List<RelatedMergeRequestSummary> collected = [];
        var truncated = false;
        await foreach (var mergeRequest in mergeRequestStream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(IssueMapper.ToRelatedMergeRequestSummary(mergeRequest));
        }

        return GitLabContent.Wrap(
            new MilestoneMergeRequestListResult(collected, truncated),
            "projects|groups/:id/milestones/:id/merge_requests");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_label
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_label", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Renames, recolors, redescribes, or archives a label at project or group scope. Only the fields supplied are changed; GitLab requires at least one.")]
    public async Task<CallToolResult> UpdateLabelAsync(
        [Description("The label's current exact name (case-sensitive).")]
        string name,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("New name, or omitted to leave unchanged.")]
        string? newName = null,
        [Description(
            "New color: a hex code such as \"#FF0000\", or one of GitLab's named CSS colors. Omit to leave unchanged.")]
        string? color = null,
        [Description("New description, or omitted to leave unchanged.")]
        string? description = null,
        [Description("New archived flag, or omitted to leave unchanged.")]
        bool? archived = null,
        [Description(
            "New priority (a lower number sorts first on the issue board). Project-scoped labels only -- omit when updating a group label. Omitted otherwise leaves unchanged.")]
        int? priority = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (group is not null && priority is not null)
            throw new McpException(
                "priority is only supported for a project-scoped label; omit it when updating a group label.");

        if (newName is null && color is null && description is null && archived is null && priority is null)
            throw new McpException(
                "Specify at least one of newName, color, description, archived, or priority to change.");

        LabelSummary summary;
        if (project is not null)
        {
            var request = new UpdateLabelRequest
            {
                NewName = newName, Color = color, Description = description, Archived = archived, Priority = priority
            };
            var label = await labelsClient.UpdateAsync(project, name, request, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }
        else
        {
            var request = new UpdateGroupLabelRequest
                { NewName = newName, Color = color, Description = description, Archived = archived };
            var label = await labelsClient.UpdateForGroupAsync(group!, name, request, cancellationToken);
            summary = LabelMapper.ToSummary(label);
        }

        return GitLabContent.Wrap(summary, "projects|groups/:id/labels/:name (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_label
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_label", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a label by exact name at project or group scope. This cannot be undone.")]
    public async Task<LabelDeleteResult> DeleteLabelAsync(
        [Description("The label's exact name (case-sensitive).")]
        string name,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (project is not null)
            await labelsClient.DeleteAsync(project, name, cancellationToken);
        else
            await labelsClient.DeleteForGroupAsync(group!, name, cancellationToken);

        return new LabelDeleteResult(name, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_promote_label
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_promote_label", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Promotes a project label to group scope, moving every issue/merge request assignment with it. Irreversible; fails if the project has no parent group.")]
    public async Task<LabelPromoteResult> PromoteLabelAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The label's exact name (case-sensitive).")]
        string name,
        CancellationToken cancellationToken = default)
    {
        await labelsClient.PromoteAsync(project, name, cancellationToken);
        return new LabelPromoteResult(name, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_board
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_board", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one issue board, including its lists, at project or group scope.")]
    public async Task<CallToolResult> GetBoardAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var board = project is not null
            ? await boards.GetForProjectAsync(project, boardId, cancellationToken)
            : await boards.GetForGroupAsync(group!, boardId, cancellationToken);

        return GitLabContent.Wrap(BoardMapper.ToDetail(board), "projects|groups/:id/boards/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_board
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_board", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Renames a board or toggles its backlog/closed-list visibility at project or group scope. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateBoardAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description("New name, or omitted to leave unchanged.")]
        string? name = null,
        [Description("If true, hide the backlog list; if false, show it. Omit to leave unchanged.")]
        bool? hideBacklogList = null,
        [Description("If true, hide the closed list; if false, show it. Omit to leave unchanged.")]
        bool? hideClosedList = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var request = new UpdateBoardRequest
            { Name = name, HideBacklogList = hideBacklogList, HideClosedList = hideClosedList };

        var board = project is not null
            ? await boards.UpdateForProjectAsync(project, boardId, request, cancellationToken)
            : await boards.UpdateForGroupAsync(group!, boardId, request, cancellationToken);

        return GitLabContent.Wrap(BoardMapper.ToSummary(board), "projects|groups/:id/boards/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_board
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_board", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes an issue board. This cannot be undone.")]
    public async Task<BoardDeleteResult> DeleteBoardAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (project is not null)
            await boards.DeleteForProjectAsync(project, boardId, cancellationToken);
        else
            await boards.DeleteForGroupAsync(group!, boardId, cancellationToken);

        return new BoardDeleteResult(boardId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_board_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_board_list", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a board list scoped to exactly one of: a label, a milestone, an iteration, or an assignee.")]
    public async Task<CallToolResult> CreateBoardListAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        [Description(
            "Numeric label id to scope the new list to. Specify exactly one of labelId/milestoneId/iterationId/assigneeId.")]
        long? labelId = null,
        [Description(
            "Numeric milestone id to scope the new list to. Specify exactly one of labelId/milestoneId/iterationId/assigneeId.")]
        long? milestoneId = null,
        [Description(
            "Numeric iteration id to scope the new list to. Specify exactly one of labelId/milestoneId/iterationId/assigneeId.")]
        long? iterationId = null,
        [Description(
            "Numeric user id to scope the new list to. Specify exactly one of labelId/milestoneId/iterationId/assigneeId.")]
        long? assigneeId = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);
        RequireExactlyOneBoardListScope(labelId, milestoneId, iterationId, assigneeId);

        var request = new CreateBoardListRequest
            { LabelId = labelId, MilestoneId = milestoneId, IterationId = iterationId, AssigneeId = assigneeId };

        var list = project is not null
            ? await boards.CreateListForProjectAsync(project, boardId, request, cancellationToken)
            : await boards.CreateListForGroupAsync(group!, boardId, request, cancellationToken);

        return GitLabContent.Wrap(BoardMapper.ToColumnSummary(list), "projects|groups/:id/boards/:id/lists (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_reorder_board_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_reorder_board_list", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Moves a board list to a new position among its siblings.")]
    public async Task<CallToolResult> ReorderBoardListAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("The list's numeric id.")]
        long listId,
        [Description("The new zero-based position for the list among its siblings.")]
        int position,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var request = new UpdateBoardListRequest { Position = position };

        var list = project is not null
            ? await boards.UpdateListPositionForProjectAsync(project, boardId, listId, request, cancellationToken)
            : await boards.UpdateListPositionForGroupAsync(group!, boardId, listId, request, cancellationToken);

        return GitLabContent.Wrap(BoardMapper.ToColumnSummary(list),
            "projects|groups/:id/boards/:id/lists/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_board_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_board_list", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a board list. This cannot be undone.")]
    public async Task<BoardListDeleteResult> DeleteBoardListAsync(
        [Description("The board's numeric id.")]
        long boardId,
        [Description("The list's numeric id.")]
        long listId,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        if (project is not null)
            await boards.DeleteListForProjectAsync(project, boardId, listId, cancellationToken);
        else
            await boards.DeleteListForGroupAsync(group!, boardId, listId, cancellationToken);

        return new BoardListDeleteResult(boardId, listId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_mark_todo_done
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_mark_todo_done", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Marks a single to-do item done. Irreversible through this API -- there is no \"mark pending\" endpoint.")]
    public async Task<CallToolResult> MarkTodoDoneAsync(
        [Description("The to-do item's numeric id, from gitlab_list_todos.")]
        long todoId,
        CancellationToken cancellationToken = default)
    {
        var todo = await todos.MarkAsDoneAsync(todoId, cancellationToken);
        return GitLabContent.Wrap(TodoMapper.ToSummary(todo), "todos/:id/mark_as_done");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_mark_all_todos_done
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_mark_all_todos_done", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description("Marks every pending to-do item done in one call. Irreversible.")]
    public async Task<MarkAllTodosDoneResult> MarkAllTodosDoneAsync(CancellationToken cancellationToken = default)
    {
        await todos.MarkAllAsDoneAsync(cancellationToken);
        return new MarkAllTodosDoneResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_merge_request_subscription
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_merge_request_subscription", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Subscribes or unsubscribes the authenticated user to a merge request's notifications. Re-applying the state the user is already in fails with a GitLab \"not modified\" response rather than succeeding as a no-op.")]
    public async Task<CallToolResult> SetMergeRequestSubscriptionAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The merge request's iid (the number shown in the GitLab UI).")]
        long mergeRequestIid,
        [Description("True to subscribe, false to unsubscribe.")]
        bool subscribe,
        CancellationToken cancellationToken = default)
    {
        var mergeRequest = subscribe
            ? await subscriptions.SubscribeToMergeRequestAsync(project, mergeRequestIid, cancellationToken)
            : await subscriptions.UnsubscribeFromMergeRequestAsync(project, mergeRequestIid, cancellationToken);

        return GitLabContent.Wrap(MergeRequestMapper.ToSubscriptionResult(mergeRequest),
            "projects/:id/merge_requests/:iid/subscribe");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_label_subscription
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_label_subscription", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Subscribes or unsubscribes the authenticated user to a project or group label's notifications. Re-applying the state the user is already in fails with a GitLab \"not modified\" response rather than succeeding as a no-op.")]
    public async Task<CallToolResult> SetLabelSubscriptionAsync(
        [Description("The label's numeric id or exact name.")]
        string labelIdOrName,
        [Description("True to subscribe, false to unsubscribe.")]
        bool subscribe,
        [Description("Project: numeric id or \"namespace/path\". Specify this or group, not both.")]
        string? project = null,
        [Description("Group: numeric id or \"namespace/path\". Specify this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        RequireExactlyOneScope(project, group);

        var label = (project is not null, subscribe) switch
        {
            (true, true) =>
                await subscriptions.SubscribeToProjectLabelAsync(project!, labelIdOrName, cancellationToken),
            (true, false) => await subscriptions.UnsubscribeFromProjectLabelAsync(project!, labelIdOrName,
                cancellationToken),
            (false, true) => await subscriptions.SubscribeToGroupLabelAsync(group!, labelIdOrName, cancellationToken),
            (false, false) => await subscriptions.UnsubscribeFromGroupLabelAsync(group!, labelIdOrName,
                cancellationToken)
        };

        return GitLabContent.Wrap(LabelMapper.ToSummary(label), "projects|groups/:id/labels/:id/subscribe");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_resource_group
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_resource_group", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets a CI/CD resource group's process mode plus which job currently holds it and which jobs are queued behind it, in one read. If no job currently holds the resource group -- the common case -- that part is simply empty; the group and its process mode are still returned.")]
    public async Task<CallToolResult> GetResourceGroupAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The resource group's key, as set by the \"resource_group\" keyword in .gitlab-ci.yml.")]
        string key,
        [Description("Maximum queued jobs to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var resourceGroup = await resourceGroups.GetAsync(project, key, cancellationToken);

        JobSummary? currentJob = null;
        try
        {
            var job = await resourceGroups.GetCurrentJobAsync(project, key, cancellationToken);
            currentJob = ResourceGroupMapper.ToJobSummary(job);
        }
        catch (GitLabNotFoundException)
        {
            // GitLab has no "idle" representation for this endpoint -- a 404 here means nothing
            // currently holds the resource group, which is the common case, not an error to surface.
            // Any other GitLabApiException propagates to the cross-cutting mapper.
        }

        List<JobSummary> upcoming = [];
        var truncated = false;
        await foreach (var job in resourceGroups.ListUpcomingJobsAsync(project, key, cancellationToken))
        {
            if (upcoming.Count == limit)
            {
                truncated = true;
                break;
            }

            upcoming.Add(ResourceGroupMapper.ToJobSummary(job));
        }

        return GitLabContent.Wrap(
            ResourceGroupMapper.ToDetail(resourceGroup, currentJob, upcoming, truncated),
            "projects/:id/resource_groups/:key (detail)");
    }

    // ---------------------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------------------

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");
    }

    /// <summary>
    ///     Enforces the project-XOR-group shape shared by every tool with both a project- and a
    ///     group-scoped form.
    /// </summary>
    private static void RequireExactlyOneScope(string? project, string? group)
    {
        if (project is null == group is null)
            throw new McpException("Specify exactly one of project or group, not both or neither.");
    }

    /// <summary>
    ///     Enforces <c>CreateBoardListRequest</c>'s "exactly one of these four" runtime contract --
    ///     GitLab rejects a body carrying none or more than one with a 400.
    /// </summary>
    private static void RequireExactlyOneBoardListScope(long? labelId, long? milestoneId, long? iterationId,
        long? assigneeId)
    {
        var setCount = (labelId is not null ? 1 : 0)
                       + (milestoneId is not null ? 1 : 0)
                       + (iterationId is not null ? 1 : 0)
                       + (assigneeId is not null ? 1 : 0);

        if (setCount != 1)
            throw new McpException("Specify exactly one of labelId, milestoneId, iterationId, or assigneeId.");
    }

    private static IReadOnlyList<string>? SplitList(string? commaSeparated)
    {
        return string.IsNullOrWhiteSpace(commaSeparated)
            ? null
            : commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<long>? SplitLongList(string? commaSeparated, string paramName)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated)) return null;

        var parts = commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<long>(parts.Length);
        foreach (var part in parts)
        {
            if (!long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new McpException(
                    $"{paramName} must be a comma-separated list of numeric ids; \"{part}\" is not numeric.");

            result.Add(value);
        }

        return result;
    }

    private static DateOnly? ParseDate(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed)) return parsed;

        throw new McpException($"{paramName} must be an ISO 8601 date (YYYY-MM-DD).");
    }

    private static GitLabIssueType? ParseIssueType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "issue" => GitLabIssueType.Issue,
            "incident" => GitLabIssueType.Incident,
            "test_case" => GitLabIssueType.TestCase,
            "requirement" => GitLabIssueType.Requirement,
            "task" => GitLabIssueType.Task,
            "ticket" => GitLabIssueType.Ticket,
            _ => throw new McpException(
                "issueType must be \"issue\", \"incident\", \"test_case\", \"requirement\", \"task\", \"ticket\", or omitted.")
        };
    }

    private static GitLabIssueSeverity? ParseIssueSeverity(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "unknown" => GitLabIssueSeverity.Unknown,
            "low" => GitLabIssueSeverity.Low,
            "medium" => GitLabIssueSeverity.Medium,
            "high" => GitLabIssueSeverity.High,
            "critical" => GitLabIssueSeverity.Critical,
            _ => throw new McpException(
                "severity must be \"unknown\", \"low\", \"medium\", \"high\", \"critical\", or omitted.")
        };
    }

    private static GitLabIterationStateFilter? ParseIterationState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "opened" => GitLabIterationStateFilter.Opened,
            "upcoming" => GitLabIterationStateFilter.Upcoming,
            "current" => GitLabIterationStateFilter.Current,
            "closed" => GitLabIterationStateFilter.Closed,
            "all" => GitLabIterationStateFilter.All,
            _ => throw new McpException(
                "state must be \"opened\", \"upcoming\", \"current\", \"closed\", \"all\", or omitted.")
        };
    }

    private static string? ParseTodoState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "pending" => "pending",
            "done" => "done",
            _ => throw new McpException("state must be \"pending\", \"done\", or omitted.")
        };
    }

    private static GitLabIssueStateFilter? ParseIssueState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "opened" => GitLabIssueStateFilter.Opened,
            "closed" => GitLabIssueStateFilter.Closed,
            "all" => GitLabIssueStateFilter.All,
            _ => throw new McpException("state must be \"opened\", \"closed\", \"all\", or omitted.")
        };
    }

    private static GitLabIssueScope? ParseIssueScope(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "authored" => GitLabIssueScope.CreatedByMe,
            "assigned_to_me" => GitLabIssueScope.AssignedToMe,
            "all" => GitLabIssueScope.All,
            _ => throw new McpException("scope must be \"authored\", \"assigned_to_me\", \"all\", or omitted.")
        };
    }

    private static GitLabIssueLinkType? ParseIssueLinkType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "relates_to" => GitLabIssueLinkType.RelatesTo,
            "blocks" => GitLabIssueLinkType.Blocks,
            "is_blocked_by" => GitLabIssueLinkType.IsBlockedBy,
            _ => throw new McpException("linkType must be \"relates_to\", \"blocks\", \"is_blocked_by\", or omitted.")
        };
    }

    private static string? ParseMilestoneState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "active" => "active",
            "closed" => "closed",
            _ => throw new McpException("state must be \"active\", \"closed\", or omitted.")
        };
    }

    private static string? ParseMilestoneStateEvent(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "close" => "close",
            "activate" => "activate",
            _ => throw new McpException("stateEvent must be \"close\", \"activate\", or omitted.")
        };
    }

    private static DateTimeOffset? ParseTimestamp(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        throw new McpException($"{paramName} must be an ISO 8601 timestamp (e.g. \"2026-09-09T00:00:00Z\").");
    }

    private static string RequireDuration(string? duration, string action)
    {
        if (string.IsNullOrWhiteSpace(duration))
            throw new McpException($"duration is required when action is \"{action}\".");

        return duration;
    }
}