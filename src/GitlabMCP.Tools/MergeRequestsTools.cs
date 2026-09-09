using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.MergeRequests;
using GitlabMCP.Mapping.MergeRequests;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Merge request lifecycle and approval-governance tools. Every payload string here originates from
///     GitLab (titles, branch names, usernames, ...), so every tool wraps its result via
///     <see cref="GitLabContent" />. Injects the narrow <c>I*Client</c> interfaces this domain spans
///     (merge requests themselves, their approvals, standing approval rules, merge trains, and external
///     status checks) rather than the root <c>IGitLabClient</c>, per CLAUDE.md's tool-authoring conventions.
/// </summary>
[McpServerToolType]
public sealed class MergeRequestsTools(
    IMergeRequestsClient mergeRequests,
    IMergeRequestApprovalsClient mergeRequestApprovals,
    IApprovalRulesClient approvalRules,
    IMergeTrainsClient mergeTrains,
    IExternalStatusChecksClient externalStatusChecks)
{
    private const int MaxLimit = 100;

    [McpServerTool(Name = "gitlab_list_group_merge_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists merge requests across a GitLab group and all of its subgroups, for a cross-project view. Returns a compact summary per merge request.")]
    public async Task<CallToolResult> ListGroupMergeRequestsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description(
            "Filter by state: \"opened\", \"closed\", \"locked\", \"merged\", or \"all\". GitLab's own default when this is omitted is \"all\" -- every state mixed together, not just open ones -- so pass \"opened\" explicitly to see only open merge requests.")]
        string? state = null,
        [Description("Optional free-text search over title and description.")]
        string? search = null,
        [Description("Maximum merge requests to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new MergeRequestListOptions
        {
            State = ParseState(state),
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<MergeRequestSummary> collected = [];
        var truncated = false;

        await foreach (var mr in mergeRequests.ListForGroupAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToSummary(mr));
        }

        return GitLabContent.Wrap(new MergeRequestListResult(collected, truncated), "groups/:id/merge_requests");
    }

    [McpServerTool(Name = "gitlab_get_approval_settings", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads a project's merge request approval policy: named approvers/groups, approvals required before merge, and governance switches such as author self-approval, committer approval, and reset-approvals-on-push.")]
    public async Task<CallToolResult> GetApprovalSettingsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var config = await mergeRequestApprovals.GetApprovalConfigurationAsync(project, cancellationToken);
        return GitLabContent.Wrap(ApprovalMapper.ToResult(config), "projects/:id/approvals");
    }

    [McpServerTool(Name = "gitlab_list_approval_rules", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the standing approval rules configured on a project, to see what approvals a merge request will need before it can merge.")]
    public async Task<CallToolResult> ListApprovalRulesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum rules to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ApprovalRuleSummary> collected = [];
        var truncated = false;

        await foreach (var rule in approvalRules.ListForProjectAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ApprovalMapper.ToSummary(rule));
        }

        return GitLabContent.Wrap(new ApprovalRuleListResult(collected, truncated), "projects/:id/approval_rules");
    }

    [McpServerTool(Name = "gitlab_list_merge_train", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the merge requests currently queued on a project's merge train(s), optionally filtered to one target branch. By default returns only cars still queued (\"active\"); pass status \"complete\" to see cars that have already merged instead.")]
    public async Task<CallToolResult> ListMergeTrainAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Optional target branch name to narrow to one merge train. Omit to see cars across every train on the project, which may mix cars from several branches.")]
        string? targetBranch = null,
        [Description(
            "Filter by queue status: \"active\" (default) for cars still queued, or \"complete\" for cars that have already merged.")]
        string status = "active",
        [Description("Maximum cars to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var scope = status.ToLowerInvariant() switch
        {
            "active" => "active",
            "complete" => "complete",
            _ => throw new McpException("status must be \"active\" or \"complete\".")
        };

        var options = new MergeTrainListOptions { Scope = scope };

        List<MergeTrainCarSummary> collected = [];
        var truncated = false;

        var cars = string.IsNullOrWhiteSpace(targetBranch)
            ? mergeTrains.ListAsync(project, options, cancellationToken)
            : mergeTrains.ListForTargetBranchAsync(project, targetBranch, options, cancellationToken);

        await foreach (var car in cars)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeTrainMapper.ToSummary(car));
        }

        return GitLabContent.Wrap(new MergeTrainListResult(collected, truncated), "projects/:id/merge_trains");
    }

    [McpServerTool(Name = "gitlab_list_external_status_checks", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the external status checks configured on a project, or -- when a merge request iid is given -- the live pass/fail/pending results of those checks against that merge request.")]
    public async Task<CallToolResult> ListExternalStatusChecksAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Optional merge request iid. When given, returns that merge request's live status check results instead of the project's configured checks.")]
        long? mergeRequestIid = null,
        [Description("Maximum checks to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ExternalStatusCheckSummary> collected = [];
        var truncated = false;

        if (mergeRequestIid is long iid)
        {
            await foreach (var check in externalStatusChecks.ListForMergeRequestAsync(project, iid, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(ExternalStatusCheckMapper.ToSummary(check));
            }

            return GitLabContent.Wrap(new ExternalStatusCheckListResult(collected, truncated),
                "projects/:id/merge_requests/:iid/status_checks");
        }

        await foreach (var check in externalStatusChecks.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ExternalStatusCheckMapper.ToSummary(check));
        }

        return GitLabContent.Wrap(new ExternalStatusCheckListResult(collected, truncated),
            "projects/:id/external_status_checks");
    }

    [McpServerTool(Name = "gitlab_list_merge_request_commits", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the commits that make up a merge request.")]
    public async Task<CallToolResult> ListMergeRequestCommitsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid (the number shown in the GitLab UI).")]
        long mergeRequestIid,
        [Description("Maximum commits to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<CommitSummary> collected = [];
        var truncated = false;

        await foreach (var commit in mergeRequests.ListCommitsAsync(project, mergeRequestIid, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToCommitSummary(commit));
        }

        return GitLabContent.Wrap(new MergeRequestCommitListResult(collected, truncated),
            "projects/:id/merge_requests/:iid/commits");
    }

    [McpServerTool(Name = "gitlab_get_merge_request_approvals", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Checks whether a merge request is approved, by whom, and which approval rules remain outstanding. Combines the approvers view and the rule-by-rule view in one call.")]
    public async Task<CallToolResult> GetMergeRequestApprovalsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid (the number shown in the GitLab UI).")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        var approvals = await mergeRequestApprovals.GetApprovalsAsync(project, mergeRequestIid, cancellationToken);
        var state = await mergeRequestApprovals.GetApprovalStateAsync(project, mergeRequestIid, cancellationToken);

        return GitLabContent.Wrap(ApprovalMapper.ToResult(approvals, state),
            "projects/:id/merge_requests/:iid/approvals");
    }

    [McpServerTool(Name = "gitlab_list_merge_request_pipelines", ReadOnly = true, OpenWorld = false)]
    [Description("Lists CI pipelines that have run for a merge request, to check merge-gate status before merging.")]
    public async Task<CallToolResult> ListMergeRequestPipelinesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("Maximum pipelines to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<PipelineSummary> collected = [];
        var truncated = false;

        await foreach (var pipeline in mergeRequests.ListPipelinesAsync(project, mergeRequestIid, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToPipelineSummary(pipeline));
        }

        return GitLabContent.Wrap(new MergeRequestPipelineListResult(collected, truncated),
            "projects/:id/merge_requests/:iid/pipelines");
    }

    [McpServerTool(Name = "gitlab_list_merge_request_related_issues", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the issues linked to a merge request: by default the issues it will close on merge, or every issue it closes or merely mentions when relation is \"related\".")]
    public async Task<CallToolResult> ListMergeRequestRelatedIssuesAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description(
            "\"closes\" (default) for issues this merge request will close on merge, or \"related\" for every issue it closes or merely mentions.")]
        string relation = "closes",
        [Description("Maximum issues to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var source = relation.ToLowerInvariant() switch
        {
            "closes" => "closes_issues",
            "related" => "related_issues",
            _ => throw new McpException("relation must be \"closes\" or \"related\".")
        };

        var issues = source == "closes_issues"
            ? mergeRequests.ListClosesIssuesAsync(project, mergeRequestIid, cancellationToken)
            : mergeRequests.ListRelatedIssuesAsync(project, mergeRequestIid, cancellationToken);

        List<IssueRefSummary> collected = [];
        var truncated = false;

        await foreach (var issue in issues)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToIssueRefSummary(issue));
        }

        return GitLabContent.Wrap(new MergeRequestIssueListResult(collected, truncated),
            $"projects/:id/merge_requests/:iid/{source}");
    }

    [McpServerTool(Name = "gitlab_list_merge_request_reviewers", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a merge request's reviewers and each one's review state (unreviewed, reviewed, requested changes).")]
    public async Task<CallToolResult> ListMergeRequestReviewersAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("Maximum reviewers to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ReviewerSummary> collected = [];
        var truncated = false;

        await foreach (var reviewer in mergeRequests.ListReviewersAsync(project, mergeRequestIid, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToReviewerSummary(reviewer));
        }

        return GitLabContent.Wrap(new MergeRequestReviewerListResult(collected, truncated),
            "projects/:id/merge_requests/:iid/reviewers");
    }

    [McpServerTool(Name = "gitlab_list_merge_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's merge requests filtered by state/labels/branch/author/search, for triage and status overview. Returns a compact summary per merge request.")]
    public async Task<CallToolResult> ListMergeRequestsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Filter by state: \"opened\", \"closed\", \"locked\", \"merged\", or \"all\". GitLab's own default when this is omitted is \"all\" -- every state mixed together, not just open ones -- so pass \"opened\" explicitly to see only open merge requests.")]
        string? state = null,
        [Description("Comma-separated label names that must all be present, e.g. \"bug,priority::1\".")]
        string? labels = null,
        [Description("Filter to merge requests from this source branch.")]
        string? sourceBranch = null,
        [Description("Filter to merge requests targeting this branch.")]
        string? targetBranch = null,
        [Description("Filter to merge requests authored by this GitLab username.")]
        string? authorUsername = null,
        [Description("Optional free-text search over title and description.")]
        string? search = null,
        [Description("Maximum merge requests to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new MergeRequestListOptions
        {
            State = ParseState(state),
            Labels = string.IsNullOrWhiteSpace(labels)
                ? null
                : labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SourceBranch = string.IsNullOrWhiteSpace(sourceBranch) ? null : sourceBranch,
            TargetBranch = string.IsNullOrWhiteSpace(targetBranch) ? null : targetBranch,
            AuthorUsername = string.IsNullOrWhiteSpace(authorUsername) ? null : authorUsername,
            Search = string.IsNullOrWhiteSpace(search) ? null : search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<MergeRequestSummary> collected = [];
        var truncated = false;

        await foreach (var mr in mergeRequests.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToSummary(mr));
        }

        return GitLabContent.Wrap(new MergeRequestListResult(collected, truncated), "projects/:id/merge_requests");
    }

    [McpServerTool(Name = "gitlab_create_merge_request", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Opens a new merge request from a source branch to a target branch with a title and optional description.")]
    public async Task<CallToolResult> CreateMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The branch containing the changes to merge.")]
        string sourceBranch,
        [Description("The branch to merge into.")]
        string targetBranch,
        [Description("The merge request's title.")]
        string title,
        [Description("Optional description in GitLab-flavored Markdown.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateMergeRequestRequest
        {
            Title = title,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            Description = string.IsNullOrWhiteSpace(description) ? null : description
        };

        var mr = await mergeRequests.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToResult(mr), "projects/:id/merge_requests (create)");
    }

    [McpServerTool(Name = "gitlab_create_approval_rule", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a standing approval rule on a project with named approvers/groups and a required approval count.")]
    public async Task<CallToolResult> CreateApprovalRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The rule's display name.")]
        string name,
        [Description("How many approvals this rule requires before it is satisfied.")]
        int approvalsRequired,
        [Description("Comma-separated numeric user ids to add as eligible approvers, or omit.")]
        string? userIds = null,
        [Description("Comma-separated numeric group ids to add as eligible approver groups, or omit.")]
        string? groupIds = null,
        [Description("Comma-separated GitLab usernames to add as eligible approvers, or omit.")]
        string? usernames = null,
        [Description("Comma-separated numeric protected-branch ids this rule applies to, or omit.")]
        string? protectedBranchIds = null,
        [Description(
            "If true, this rule applies to every protected branch on the project instead of only the ones in protectedBranchIds. Default false.")]
        bool appliesToAllProtectedBranches = false,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateApprovalRuleRequest
        {
            Name = name,
            ApprovalsRequired = approvalsRequired,
            UserIds = ParseLongList(userIds, nameof(userIds)),
            GroupIds = ParseLongList(groupIds, nameof(groupIds)),
            Usernames = ParseStringList(usernames),
            ProtectedBranchIds = ParseLongList(protectedBranchIds, nameof(protectedBranchIds)),
            AppliesToAllProtectedBranches = appliesToAllProtectedBranches
        };

        var rule = await approvalRules.CreateForProjectAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ApprovalMapper.ToSummary(rule), "projects/:id/approval_rules (create)");
    }

    [McpServerTool(Name = "gitlab_approve_merge_request", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Approves a merge request as the authenticated user.")]
    public async Task<CallToolResult> ApproveMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description(
            "Optional: the exact commit SHA currently at the head of the merge request, to guard against approving a version you have not seen. Omit to skip this check.")]
        string? sha = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ApproveMergeRequestRequest { Sha = string.IsNullOrWhiteSpace(sha) ? null : sha };
        var approvals = await mergeRequestApprovals.ApproveAsync(project, mergeRequestIid, request, cancellationToken);
        return GitLabContent.Wrap(ApprovalMapper.ToActionResult(approvals), "projects/:id/merge_requests/:iid/approve");
    }

    [McpServerTool(Name = "gitlab_add_to_merge_train", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Queues a merge request onto its target branch's merge train.")]
    public async Task<CallToolResult> AddToMergeTrainAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description(
            "Optional: the exact commit SHA currently at the head of the merge request, to guard against queuing a version you have not seen. Omit to skip this check.")]
        string? sha = null,
        [Description(
            "If true, squash the merge request's commits into one when it merges. Omit to use the project's default.")]
        bool? squash = null,
        [Description(
            "If true, merge immediately once this car reaches the front of the train instead of waiting for other cars. Omit to use the train's default behavior.")]
        bool? autoMerge = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AddToMergeTrainRequest
        {
            Sha = string.IsNullOrWhiteSpace(sha) ? null : sha,
            Squash = squash,
            AutoMerge = autoMerge
        };

        var car = await mergeTrains.AddMergeRequestAsync(project, mergeRequestIid, request, cancellationToken);
        return GitLabContent.Wrap(MergeTrainMapper.ToSummary(car), "projects/:id/merge_trains (create)");
    }

    [McpServerTool(Name = "gitlab_update_merge_request", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Edits a merge request's title, description, target branch, labels, assignees, reviewers or milestone, or closes/reopens it. Only the fields you supply are changed; everything else is left as it is.")]
    public async Task<CallToolResult> UpdateMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("New title, or omitted to leave unchanged.")]
        string? title = null,
        [Description("New description, or omitted to leave unchanged.")]
        string? description = null,
        [Description("New target branch, or omitted to leave unchanged.")]
        string? targetBranch = null,
        [Description("\"close\" or \"reopen\", or omitted to leave the open/closed state unchanged.")]
        string? stateEvent = null,
        [Description(
            "Comma-separated numeric user ids to set as the sole assignees, replacing the existing list entirely. Omit to leave assignees unchanged.")]
        string? assigneeIds = null,
        [Description(
            "Comma-separated numeric user ids to set as the sole reviewers, replacing the existing list entirely. Omit to leave reviewers unchanged.")]
        string? reviewerIds = null,
        [Description(
            "Comma-separated label names that replace the merge request's entire label set. Omit to leave labels unchanged; use addLabels/removeLabels instead for an incremental change.")]
        string? labels = null,
        [Description("Comma-separated label names to add without disturbing the rest of the labels. Omit for none.")]
        string? addLabels = null,
        [Description("Comma-separated label names to remove without disturbing the rest of the labels. Omit for none.")]
        string? removeLabels = null,
        [Description("Numeric id of the milestone to set, or omit to leave the milestone unchanged.")]
        long? milestoneId = null,
        [Description("If true, delete the source branch once the merge request merges. Omit to leave unchanged.")]
        bool? removeSourceBranch = null,
        [Description("If true, squash the merge request's commits into one when it merges. Omit to leave unchanged.")]
        bool? squash = null,
        CancellationToken cancellationToken = default)
    {
        var gqlStateEvent = stateEvent?.ToLowerInvariant() switch
        {
            "close" => MergeRequestStateEvent.Close,
            "reopen" => MergeRequestStateEvent.Reopen,
            null or "" => (MergeRequestStateEvent?)null,
            _ => throw new McpException("stateEvent must be \"close\", \"reopen\", or omitted.")
        };

        var request = new UpdateMergeRequestRequest
        {
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            TargetBranch = string.IsNullOrWhiteSpace(targetBranch) ? null : targetBranch,
            StateEvent = gqlStateEvent,
            AssigneeIds = ParseLongList(assigneeIds, nameof(assigneeIds)),
            ReviewerIds = ParseLongList(reviewerIds, nameof(reviewerIds)),
            Labels = ParseStringList(labels),
            AddLabels = ParseStringList(addLabels),
            RemoveLabels = ParseStringList(removeLabels),
            MilestoneId = milestoneId,
            RemoveSourceBranch = removeSourceBranch,
            Squash = squash
        };

        var mr = await mergeRequests.UpdateAsync(project, mergeRequestIid, request, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToResult(mr), "projects/:id/merge_requests/:iid (update)");
    }

    [McpServerTool(Name = "gitlab_update_approval_rule", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a standing approval rule's approvers or required count. Approver lists you supply are authoritative and replace the rule's existing list entirely; omit a list to leave that part of the rule unchanged.")]
    public async Task<CallToolResult> UpdateApprovalRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The approval rule's numeric id, from gitlab_list_approval_rules or gitlab_create_approval_rule.")]
        long approvalRuleId,
        [Description("New display name, or omit to leave unchanged.")]
        string? name = null,
        [Description("New required-approval count, or omit to leave unchanged.")]
        int? approvalsRequired = null,
        [Description(
            "Comma-separated numeric user ids that replace the rule's entire eligible-approver user list, or omit to leave it unchanged.")]
        string? userIds = null,
        [Description(
            "Comma-separated numeric group ids that replace the rule's entire eligible-approver group list, or omit to leave it unchanged.")]
        string? groupIds = null,
        [Description(
            "Comma-separated GitLab usernames that replace the rule's entire eligible-approver username list, or omit to leave it unchanged.")]
        string? usernames = null,
        [Description(
            "Comma-separated numeric protected-branch ids that replace the rule's branch scope, or omit to leave it unchanged.")]
        string? protectedBranchIds = null,
        [Description(
            "If true, this rule applies to every protected branch on the project instead of only the ones in protectedBranchIds. Omit to leave unchanged.")]
        bool? appliesToAllProtectedBranches = null,
        [Description(
            "If true, remove any hidden (invisible-to-you) groups currently on the rule instead of preserving them. Omit to leave unchanged.")]
        bool? removeHiddenGroups = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateApprovalRuleRequest
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            ApprovalsRequired = approvalsRequired,
            UserIds = ParseLongList(userIds, nameof(userIds)),
            GroupIds = ParseLongList(groupIds, nameof(groupIds)),
            Usernames = ParseStringList(usernames),
            ProtectedBranchIds = ParseLongList(protectedBranchIds, nameof(protectedBranchIds)),
            AppliesToAllProtectedBranches = appliesToAllProtectedBranches,
            RemoveHiddenGroups = removeHiddenGroups
        };

        var rule = await approvalRules.UpdateForProjectAsync(project, approvalRuleId, request, cancellationToken);
        return GitLabContent.Wrap(ApprovalMapper.ToSummary(rule), "projects/:id/approval_rules/:id (update)");
    }

    [McpServerTool(Name = "gitlab_unapprove_merge_request", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Withdraws the authenticated user's own approval from a merge request.")]
    public async Task<CallToolResult> UnapproveMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        var approvals = await mergeRequestApprovals.UnapproveAsync(project, mergeRequestIid, cancellationToken);
        return GitLabContent.Wrap(ApprovalMapper.ToActionResult(approvals),
            "projects/:id/merge_requests/:iid/unapprove");
    }

    [McpServerTool(Name = "gitlab_list_my_merge_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists merge requests scoped to the authenticated user across the whole GitLab instance -- the \"what's on my plate\" query. Returns a compact summary per merge request.")]
    public async Task<CallToolResult> ListMyMergeRequestsAsync(
        [Description(
            "Which merge requests to return: \"assigned_to_me\" (default), \"created_by_me\", \"reviews_for_me\", or \"all\" for everything the token can see.")]
        string scope = "assigned_to_me",
        [Description(
            "Filter by state: \"opened\", \"closed\", \"locked\", \"merged\", or \"all\". GitLab's own default when this is omitted is \"all\" -- every state mixed together, not just open ones -- so pass \"opened\" explicitly to triage only open merge requests.")]
        string? state = null,
        [Description("Maximum merge requests to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new MergeRequestListOptions
        {
            Scope = ParseScope(scope),
            State = ParseState(state),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<MergeRequestSummary> collected = [];
        var truncated = false;

        await foreach (var mr in mergeRequests.ListAllAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToSummary(mr));
        }

        return GitLabContent.Wrap(new MergeRequestListResult(collected, truncated), "merge_requests");
    }

    [McpServerTool(Name = "gitlab_get_merge_request", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one merge request's full detail, including its description, by project and iid.")]
    public async Task<CallToolResult> GetMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid (the number shown in the GitLab UI).")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        var mr = await mergeRequests.GetAsync(project, mergeRequestIid, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToResult(mr), "projects/:id/merge_requests/:iid");
    }

    [McpServerTool(Name = "gitlab_merge_merge_request", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Merges a merge request, optionally squashing its commits or supplying custom merge/squash commit messages. This is irreversible through the API -- there is no unmerge.")]
    public async Task<CallToolResult> MergeMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("Optional custom merge commit message. Omit to use GitLab's default.")]
        string? mergeCommitMessage = null,
        [Description(
            "Optional custom squash commit message, used only when squash is true. Omit to use GitLab's default.")]
        string? squashCommitMessage = null,
        [Description(
            "If true, squash the merge request's commits into one when it merges. Omit to use the project's default.")]
        bool? squash = null,
        [Description("If true, delete the source branch once merged. Omit to use the project's default.")]
        bool? shouldRemoveSourceBranch = null,
        [Description(
            "Optional: the exact commit SHA currently at the head of the merge request, to guard against merging a version you have not seen. Omit to skip this check.")]
        string? sha = null,
        CancellationToken cancellationToken = default)
    {
        var request = new MergeMergeRequestRequest
        {
            MergeCommitMessage = string.IsNullOrWhiteSpace(mergeCommitMessage) ? null : mergeCommitMessage,
            SquashCommitMessage = string.IsNullOrWhiteSpace(squashCommitMessage) ? null : squashCommitMessage,
            Squash = squash,
            ShouldRemoveSourceBranch = shouldRemoveSourceBranch,
            Sha = string.IsNullOrWhiteSpace(sha) ? null : sha
        };

        var mr = await mergeRequests.MergeAsync(project, mergeRequestIid, request, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToResult(mr), "projects/:id/merge_requests/:iid/merge");
    }

    [McpServerTool(Name = "gitlab_delete_merge_request", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes a merge request outright. This is not the same as closing it, and cannot be undone.")]
    public async Task<MergeRequestDeleteResult> DeleteMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        await mergeRequests.DeleteAsync(project, mergeRequestIid, cancellationToken);
        return new MergeRequestDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_rebase_merge_request", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Queues a rebase of the merge request's source branch onto its target branch to resolve conflicts. GitLab schedules the rebase and answers immediately -- the result only says whether the rebase started, not whether it finished -- and this rewrites the source branch's history.")]
    public async Task<CallToolResult> RebaseMergeRequestAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("If true, mark the rebase commit so it does not trigger a new pipeline. Default false.")]
        bool skipCi = false,
        CancellationToken cancellationToken = default)
    {
        var request = new RebaseMergeRequestRequest { SkipCi = skipCi };
        var result = await mergeRequests.RebaseAsync(project, mergeRequestIid, request, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToRebaseResult(result), "projects/:id/merge_requests/:iid/rebase");
    }

    [McpServerTool(Name = "gitlab_cancel_merge_when_pipeline_succeeds", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Cancels a previously scheduled auto-merge-on-green-pipeline for a merge request, without closing or merging it.")]
    public async Task<CallToolResult> CancelMergeWhenPipelineSucceedsAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        var mr = await mergeRequests.CancelMergeWhenPipelineSucceedsAsync(project, mergeRequestIid, cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToResult(mr),
            "projects/:id/merge_requests/:iid/cancel_merge_when_pipeline_succeeds");
    }

    [McpServerTool(Name = "gitlab_get_merge_request_diff", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets the per-file diffs of a merge request, bounded by limit, for code review without pulling the whole raw patch. Each file's diff text is capped independently; the result says whether any file was capped, whether GitLab itself withheld a file's diff for being oversized, and whether more files exist beyond limit.")]
    public async Task<CallToolResult> GetMergeRequestDiffAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        [Description("Maximum files to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new MergeRequestDiffListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<DiffFileSummary> collected = [];
        var truncated = false;

        await foreach (var diff in mergeRequests.ListDiffsAsync(project, mergeRequestIid, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(MergeRequestMapper.ToDiffFileSummary(diff));
        }

        return GitLabContent.Wrap(new MergeRequestDiffListResult(collected, truncated),
            "projects/:id/merge_requests/:iid/diffs");
    }

    [McpServerTool(Name = "gitlab_run_merge_request_pipeline", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Triggers a new CI pipeline run for a merge request, e.g. to retry after fixing a failure.")]
    public async Task<CallToolResult> RunMergeRequestPipelineAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The merge request's project-scoped iid.")]
        long mergeRequestIid,
        CancellationToken cancellationToken = default)
    {
        var pipeline = await mergeRequests.CreatePipelineAsync(project, mergeRequestIid,
            new CreateMergeRequestPipelineRequest(), cancellationToken);
        return GitLabContent.Wrap(MergeRequestMapper.ToPipelineSummary(pipeline),
            "projects/:id/merge_requests/:iid/pipelines (create)");
    }

    [McpServerTool(Name = "gitlab_delete_approval_rule", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a project's standing approval rule. Project scope only -- there is no group-rule delete. This cannot be undone.")]
    public async Task<ApprovalRuleDeleteResult> DeleteApprovalRuleAsync(
        [Description(
            "Project: either the numeric project id (\"42\") or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("The approval rule's numeric id, from gitlab_list_approval_rules or gitlab_create_approval_rule.")]
        long approvalRuleId,
        CancellationToken cancellationToken = default)
    {
        await approvalRules.DeleteForProjectAsync(project, approvalRuleId, cancellationToken);
        return new ApprovalRuleDeleteResult(true);
    }

    private static MergeRequestStateFilter? ParseState(string? state)
    {
        return state?.ToLowerInvariant() switch
        {
            null or "" => null,
            "opened" => MergeRequestStateFilter.Opened,
            "closed" => MergeRequestStateFilter.Closed,
            "locked" => MergeRequestStateFilter.Locked,
            "merged" => MergeRequestStateFilter.Merged,
            "all" => MergeRequestStateFilter.All,
            _ => throw new McpException(
                "state must be \"opened\", \"closed\", \"locked\", \"merged\", \"all\", or omitted.")
        };
    }

    private static MergeRequestScope ParseScope(string scope)
    {
        return scope.ToLowerInvariant() switch
        {
            "created_by_me" => MergeRequestScope.CreatedByMe,
            "assigned_to_me" => MergeRequestScope.AssignedToMe,
            "reviews_for_me" => MergeRequestScope.ReviewsForMe,
            "all" => MergeRequestScope.All,
            _ => throw new McpException(
                "scope must be \"created_by_me\", \"assigned_to_me\", \"reviews_for_me\", or \"all\".")
        };
    }

    private static IReadOnlyList<long>? ParseLongList(string? csv, string paramName)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<long>(parts.Length);

        foreach (var part in parts)
        {
            if (!long.TryParse(part, out var value))
                throw new McpException(
                    $"{paramName} must be a comma-separated list of numeric ids; \"{part}\" is not a number.");

            result.Add(value);
        }

        return result;
    }

    private static IReadOnlyList<string>? ParseStringList(string? csv)
    {
        return string.IsNullOrWhiteSpace(csv)
            ? null
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}