using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Domain;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Code;
using GitlabMCP.Mapping.Code;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Repository/files/branches/commits tools (domain "code"). Every tool here returns GitLab-authored text
///     (commit messages, author identities, branch/tag names, regex configuration, …) so every tool declares
///     <c>Task&lt;CallToolResult&gt;</c> and wraps via <see cref="GitLabContent" />.
/// </summary>
[McpServerToolType]
public sealed class CodeTools(
    ICommitsClient commits,
    IRepositoriesClient repositories,
    IRepositoryFilesClient repositoryFiles,
    IBranchesClient branches,
    ITagsClient tags,
    ICommitStatusesClient commitStatuses,
    IProtectedBranchesClient protectedBranches,
    IProtectedTagsClient protectedTags,
    IPushRulesClient pushRules,
    IProjectMirrorsClient projectMirrors,
    IRemoteMirrorsClient remoteMirrors)
{
    private const int MaxLimit = 100;

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_commit_comments", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the line- or commit-level comments left on one commit.")]
    public async Task<CallToolResult> ListCommitCommentsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description("Maximum comments to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<CommitCommentSummary> collected = [];
        var truncated = false;

        await foreach (var comment in commits.ListCommentsAsync((ProjectId)project, sha, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(comment));
        }

        return GitLabContent.Wrap(new CommitCommentListResult(collected, truncated),
            "projects/:id/repository/commits/:sha/comments");
    }

    [McpServerTool(Name = "gitlab_list_repository_contributors", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists per-author commit and line-change counts for a project's repository, for activity or ownership questions.")]
    public async Task<CallToolResult> ListRepositoryContributorsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Sort field: \"name\", \"email\", or \"commits\". Omit for GitLab's default.")]
        string? orderBy = null,
        [Description("Sort direction: \"asc\" or \"desc\". Omit for GitLab's default.")]
        string? sort = null,
        [Description("Maximum contributors to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ContributorListOptions
        {
            OrderBy = orderBy,
            Sort = sort,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ContributorSummary> collected = [];
        var truncated = false;

        await foreach (var contributor in repositories.ListContributorsAsync((ProjectId)project, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(contributor));
        }

        return GitLabContent.Wrap(new ContributorListResult(collected, truncated),
            "projects/:id/repository/contributors");
    }

    [McpServerTool(Name = "gitlab_get_file_blame", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Finds which commit and author last touched each line of a file, optionally scoped to a line range. Returns one entry per run of contiguous lines sharing a commit. Each range's line text is truncated at 200 lines if the run is very long (see linesTruncated on the range).")]
    public async Task<CallToolResult> GetFileBlameAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Full path from the repository root, e.g. \"src/Program.cs\".")]
        string filePath,
        [Description("The branch, tag or commit SHA to blame at.")]
        string refName,
        [Description(
            "First line to blame (1-based). Supply together with rangeEnd, or omit both to blame the whole file.")]
        int? rangeStart = null,
        [Description("Last line to blame (1-based). Supply together with rangeStart.")]
        int? rangeEnd = null,
        [Description(
            "Maximum blame ranges to return (1-100). Default 100 — a range covers a run of several lines, not one line each.")]
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (rangeStart is null != rangeEnd is null)
            throw new McpException("rangeStart and rangeEnd must be supplied together, or both omitted.");

        if (rangeStart is not null && rangeEnd is not null && rangeStart > rangeEnd)
            throw new McpException("rangeStart must be less than or equal to rangeEnd.");

        List<BlameRangeSummary> collected = [];
        var truncated = false;

        await foreach (var range in repositoryFiles.GetBlameAsync((ProjectId)project, filePath, refName, rangeStart,
                           rangeEnd, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RepositoryMapper.ToSummary(range));
        }

        return GitLabContent.Wrap(new BlameResult(collected, truncated),
            "projects/:id/repository/files/:file_path/blame");
    }

    [McpServerTool(Name = "gitlab_list_branches", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's branches, optionally filtered by a search substring or a regular expression.")]
    public async Task<CallToolResult> ListBranchesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Only return branches whose name contains this substring. Omit for all.")]
        string? search = null,
        [Description("Only return branches whose name matches this regular expression. Omit for all.")]
        string? regex = null,
        [Description("Maximum branches to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new BranchListOptions
        {
            Search = search,
            Regex = regex,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<BranchSummary> collected = [];
        var truncated = false;

        await foreach (var branch in branches.ListAsync((ProjectId)project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RepositoryMapper.ToSummary(branch));
        }

        return GitLabContent.Wrap(new BranchListResult(collected, truncated), "projects/:id/repository/branches");
    }

    [McpServerTool(Name = "gitlab_list_tags", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's repository tags.")]
    public async Task<CallToolResult> ListTagsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Only return tags whose name contains this substring. Omit for all.")]
        string? search = null,
        [Description("Maximum tags to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new TagListOptions
        {
            Search = search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<TagSummary> collected = [];
        var truncated = false;

        await foreach (var tag in tags.ListAsync((ProjectId)project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RepositoryMapper.ToSummary(tag));
        }

        return GitLabContent.Wrap(new TagListResult(collected, truncated), "projects/:id/repository/tags");
    }

    [McpServerTool(Name = "gitlab_list_commit_statuses", ReadOnly = true, OpenWorld = false)]
    [Description("Lists what CI systems (GitLab's own pipelines or external integrations) have reported for a commit.")]
    public async Task<CallToolResult> ListCommitStatusesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description("If true, includes every retried status, not just the latest per name. Default false.")]
        bool all = false,
        [Description("Maximum statuses to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new CommitStatusListOptions
        {
            All = all,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<CommitStatusSummary> collected = [];
        var truncated = false;

        await foreach (var status in commitStatuses.ListAsync((ProjectId)project, sha, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(status));
        }

        return GitLabContent.Wrap(new CommitStatusListResult(collected, truncated),
            "projects/:id/repository/commits/:sha/statuses");
    }

    [McpServerTool(Name = "gitlab_list_protected_branches", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists which branches or wildcard patterns are protected on a project, and who may push, merge or unprotect them.")]
    public async Task<CallToolResult> ListProtectedBranchesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum protected branches to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ProtectedBranchSummary> collected = [];
        var truncated = false;

        await foreach (var branch in protectedBranches.ListAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectionMapper.ToSummary(branch));
        }

        return GitLabContent.Wrap(new ProtectedBranchListResult(collected, truncated),
            "projects/:id/protected_branches");
    }

    [McpServerTool(Name = "gitlab_list_protected_tags", ReadOnly = true, OpenWorld = false)]
    [Description("Lists which tags or wildcard patterns are protected on a project, and who may create matching tags.")]
    public async Task<CallToolResult> ListProtectedTagsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum protected tags to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ProtectedTagSummary> collected = [];
        var truncated = false;

        await foreach (var tag in protectedTags.ListAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectionMapper.ToSummary(tag));
        }

        return GitLabContent.Wrap(new ProtectedTagListResult(collected, truncated), "projects/:id/protected_tags");
    }

    [McpServerTool(Name = "gitlab_get_push_rule", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads a project's push rule: commit-message/branch-name regexes, secret detection, and signing requirements. Fails with a not-found error if the project has no push rule configured.")]
    public async Task<CallToolResult> GetPushRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var rule = await pushRules.GetForProjectAsync((ProjectId)project, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(rule), "projects/:id/push_rule");
    }

    [McpServerTool(Name = "gitlab_get_pull_mirror", ReadOnly = true, OpenWorld = false)]
    [Description("Checks a project's pull-mirror configuration and last sync status.")]
    public async Task<CallToolResult> GetPullMirrorAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var mirror = await projectMirrors.GetAsync((ProjectId)project, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(mirror), "projects/:id/mirror/pull");
    }

    [McpServerTool(Name = "gitlab_list_repository_tree", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Browses files and directories in a project's repository at a given ref, to locate a path before reading or editing it.")]
    public async Task<CallToolResult> ListRepositoryTreeAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Subdirectory to list from the repository root, e.g. \"src\". Omit to list from the root.")]
        string? path = null,
        [Description("The branch, tag or commit SHA to browse. Omit for the project's default branch.")]
        string? refName = null,
        [Description(
            "If true, lists every file and directory under path recursively instead of just its immediate children. Default false.")]
        bool recursive = false,
        [Description("Maximum entries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new TreeListOptions
        {
            Path = path,
            Ref = refName,
            Recursive = recursive,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<TreeItemSummary> collected = [];
        var truncated = false;

        await foreach (var item in repositories.ListTreeAsync((ProjectId)project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RepositoryMapper.ToSummary(item));
        }

        return GitLabContent.Wrap(new TreeListResult(collected, truncated), "projects/:id/repository/tree");
    }

    [McpServerTool(Name = "gitlab_get_file_content", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads a file's text content, decoded from Base64, plus size/blob-id/last-commit metadata at a branch, tag or commit SHA. Content is truncated at 20000 characters if the file is very large (see contentTruncated on the result).")]
    public async Task<CallToolResult> GetFileContentAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Full path from the repository root, e.g. \"src/Program.cs\".")]
        string filePath,
        [Description("The branch, tag or commit SHA to read at.")]
        string refName,
        CancellationToken cancellationToken = default)
    {
        var file = await repositoryFiles.GetAsync((ProjectId)project, filePath, refName, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToContentSummary(file), "projects/:id/repository/files/:file_path");
    }

    [McpServerTool(Name = "gitlab_list_commits", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's commit history, newest first, optionally scoped to a branch/tag and a date range.")]
    public async Task<CallToolResult> ListCommitsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "Only commits reachable from this branch, tag or commit SHA. Omit for the project's default branch.")]
        string? refName = null,
        [Description(
            "Only commits authored on or after this date/time (ISO 8601, e.g. \"2026-01-01T00:00:00Z\"). Omit for no lower bound.")]
        DateTimeOffset? since = null,
        [Description("Only commits authored on or before this date/time (ISO 8601). Omit for no upper bound.")]
        DateTimeOffset? until = null,
        [Description("Maximum commits to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new CommitListOptions
        {
            RefName = refName,
            Since = since,
            Until = until,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<CommitSummary> collected = [];
        var truncated = false;

        await foreach (var commit in commits.ListAsync((ProjectId)project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(commit));
        }

        return GitLabContent.Wrap(new CommitListResult(collected, truncated), "projects/:id/repository/commits");
    }

    [McpServerTool(Name = "gitlab_get_commit", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Fetches one commit's message, author, committer and (by default) line-change stats, by SHA, branch or tag name.")]
    public async Task<CallToolResult> GetCommitAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA, or a branch/tag name.")]
        string sha,
        [Description("If false, omits the additions/deletions/total line-change counts. Default true.")]
        bool includeStats = true,
        CancellationToken cancellationToken = default)
    {
        var commit = await commits.GetAsync((ProjectId)project, sha, includeStats, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToDetailSummary(commit), "projects/:id/repository/commits/:sha");
    }

    [McpServerTool(Name = "gitlab_get_commit_diff", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Shows the per-file diff a single commit introduced. Each file's diff text is truncated independently if very large.")]
    public async Task<CallToolResult> GetCommitDiffAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description("Maximum changed files to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new CommitDiffOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<DiffSummary> collected = [];
        var truncated = false;

        await foreach (var diff in commits.ListDiffsAsync((ProjectId)project, sha, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(diff));
        }

        return GitLabContent.Wrap(new DiffListResult(collected, truncated),
            "projects/:id/repository/commits/:sha/diff");
    }

    [McpServerTool(Name = "gitlab_list_commit_merge_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Finds which merge request(s) a given commit landed through, for traceability from a hotfix or cherry-pick.")]
    public async Task<CallToolResult> ListCommitMergeRequestsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description("Filter by state: \"opened\", \"closed\", \"locked\", or \"merged\". Omit for all.")]
        string? state = null,
        [Description("Maximum merge requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new CommitMergeRequestListOptions
        {
            State = state,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<CommitMergeRequestSummary> collected = [];
        var truncated = false;

        await foreach (var mergeRequest in commits.ListMergeRequestsAsync((ProjectId)project, sha, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(CommitMapper.ToSummary(mergeRequest));
        }

        return GitLabContent.Wrap(new CommitMergeRequestListResult(collected, truncated),
            "projects/:id/repository/commits/:sha/merge_requests");
    }

    [McpServerTool(Name = "gitlab_get_branch", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one branch's head commit and coarse protection flags.")]
    public async Task<CallToolResult> GetBranchAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The branch's exact name.")]
        string branchName,
        CancellationToken cancellationToken = default)
    {
        var branch = await branches.GetAsync((ProjectId)project, branchName, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToSummary(branch), "projects/:id/repository/branches/:branch");
    }

    [McpServerTool(Name = "gitlab_compare_refs", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Shows the commits and per-file diffs between two branches, tags or commits — e.g. what a merge would bring in. Both lists are independently bounded by limit.")]
    public async Task<CallToolResult> CompareRefsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The starting branch, tag or commit SHA.")]
        string fromRef,
        [Description("The ending branch, tag or commit SHA.")]
        string toRef,
        [Description(
            "If true, compares fromRef..toRef directly instead of GitLab's default merge-base comparison (fromRef...toRef). Default false.")]
        bool straight = false,
        [Description(
            "Maximum commits and maximum diff entries to return, each bounded independently (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var compare = await repositories.CompareAsync((ProjectId)project, fromRef, toRef, null, straight, null,
            cancellationToken);

        var allCommits = compare.Commits ?? [];
        var commitsTruncated = allCommits.Count > limit;
        var boundedCommits = allCommits.Take(limit).Select(CommitMapper.ToSummary).ToList();

        var allDiffs = compare.Diffs ?? [];
        var diffsTruncated = allDiffs.Count > limit;
        var boundedDiffs = allDiffs.Take(limit).Select(CommitMapper.ToSummary).ToList();

        var result = new CompareResult(
            boundedCommits,
            commitsTruncated,
            boundedDiffs,
            diffsTruncated,
            compare.CompareTimeout,
            compare.CompareSameRef,
            compare.WebUrl?.ToString());

        return GitLabContent.Wrap(result, "projects/:id/repository/compare");
    }

    [McpServerTool(Name = "gitlab_list_push_mirrors", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's configured push mirrors — remotes GitLab pushes every update to — along with each one's last sync status.")]
    public async Task<CallToolResult> ListPushMirrorsAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Maximum push mirrors to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<RemoteMirrorSummary> collected = [];
        var truncated = false;

        await foreach (var mirror in remoteMirrors.ListAsync((ProjectId)project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RemoteMirrorMapper.ToSummary(mirror));
        }

        return GitLabContent.Wrap(new RemoteMirrorListResult(collected, truncated), "projects/:id/remote_mirrors");
    }

    // ---------------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_commit", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Commits several file create/update/delete/move/chmod actions atomically in one push, without a local git checkout. All actions land in a single commit.")]
    public async Task<CallToolResult> CreateCommitAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The branch to commit onto. Must already exist unless startBranch is also given.")]
        string branch,
        [Description("The commit message.")] string commitMessage,
        [Description("One or more file operations to apply atomically. At least one is required.")]
        IReadOnlyList<CommitFileAction> actions,
        [Description(
            "If set, creates \"branch\" fresh from this existing branch/tag/SHA instead of requiring it to already exist.")]
        string? startBranch = null,
        [Description("Overrides the commit author's email. Omit to use the token's own identity.")]
        string? authorEmail = null,
        [Description("Overrides the commit author's name. Omit to use the token's own identity.")]
        string? authorName = null,
        [Description(
            "If true, force-pushes the commit, discarding any commits already on the target branch that this one does not build on. Default false.")]
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (actions.Count == 0) throw new McpException("actions must contain at least one file operation.");

        var request = new CreateCommitRequest
        {
            Branch = branch,
            CommitMessage = commitMessage,
            StartBranch = startBranch,
            AuthorEmail = authorEmail,
            AuthorName = authorName,
            Force = force,
            Actions = actions.Select(ToCommitAction).ToList()
        };

        var commit = await commits.CreateAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToSummary(commit), "projects/:id/repository/commits (create)");
    }

    [McpServerTool(Name = "gitlab_create_file", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new file and commits it directly to a branch, no local checkout needed. Fails if the file already exists.")]
    public async Task<CallToolResult> CreateFileAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Full path from the repository root for the new file, e.g. \"docs/README.md\".")]
        string filePath,
        [Description("The branch to commit the new file to. Must already exist.")]
        string branch,
        [Description("The file's content.")] string content,
        [Description("The commit message.")] string commitMessage,
        [Description("Set to \"base64\" if content is Base64-encoded binary data. Omit for plain text.")]
        string? encoding = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateRepositoryFileRequest
        {
            Branch = branch,
            Content = content,
            CommitMessage = commitMessage,
            Encoding = encoding
        };

        var file = await repositoryFiles.CreateAsync((ProjectId)project, filePath, request, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToSummary(file),
            "projects/:id/repository/files/:file_path (create)");
    }

    [McpServerTool(Name = "gitlab_create_branch", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new branch from an existing ref (branch, tag, or commit SHA).")]
    public async Task<CallToolResult> CreateBranchAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Name for the new branch.")]
        string branchName,
        [Description("The branch, tag or commit SHA to branch from.")]
        string refName,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBranchRequest { Branch = branchName, Ref = refName };
        var branch = await branches.CreateAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToSummary(branch), "projects/:id/repository/branches (create)");
    }

    [McpServerTool(Name = "gitlab_create_tag", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Tags a ref (branch, tag, or commit SHA), optionally as an annotated tag carrying a release message.")]
    public async Task<CallToolResult> CreateTagAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Name for the new tag.")] string tagName,
        [Description("The branch, tag or commit SHA to tag.")]
        string refName,
        [Description(
            "Optional annotation message. Supplying one creates an annotated tag instead of a lightweight one.")]
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateTagRequest { TagName = tagName, Ref = refName, Message = message };
        var tag = await tags.CreateAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToSummary(tag), "projects/:id/repository/tags (create)");
    }

    [McpServerTool(Name = "gitlab_protect_branch", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Protects a branch or wildcard pattern (e.g. \"release-*\") with role-based push/merge/unprotect rules. Fails if the branch (or pattern) is already protected — use no equivalent update tool in this profile without unprotecting first.")]
    public async Task<CallToolResult> ProtectBranchAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Exact branch name, or a wildcard pattern such as \"release-*\".")]
        string branchName,
        [Description(
            "Minimum role allowed to push directly, as a GitLab access level: 0=No access, 30=Developer, 40=Maintainer, 60=Admin. Omit for GitLab's default (Maintainer).")]
        int? pushAccessLevel = null,
        [Description(
            "Minimum role allowed to merge into this branch, as a GitLab access level (see pushAccessLevel). Omit for GitLab's default (Maintainer).")]
        int? mergeAccessLevel = null,
        [Description(
            "Minimum role allowed to unprotect this branch, as a GitLab access level (see pushAccessLevel). Omit for GitLab's default (Maintainer).")]
        int? unprotectAccessLevel = null,
        [Description("If true, allows force-pushes from users who can push to this branch. Default false.")]
        bool allowForcePush = false,
        [Description(
            "If true, a merge request targeting this branch needs approval from a Code Owner of the changed files. Default false.")]
        bool codeOwnerApprovalRequired = false,
        CancellationToken cancellationToken = default)
    {
        var request = new ProtectBranchRequest
        {
            Name = branchName,
            AllowForcePush = allowForcePush,
            PushAccessLevel = pushAccessLevel,
            MergeAccessLevel = mergeAccessLevel,
            UnprotectAccessLevel = unprotectAccessLevel,
            CodeOwnerApprovalRequired = codeOwnerApprovalRequired
        };

        var protectedBranch = await protectedBranches.ProtectAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(protectedBranch),
            "projects/:id/protected_branches (create)");
    }

    [McpServerTool(Name = "gitlab_protect_tag", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Protects a tag or wildcard pattern (e.g. \"v*\") so only a chosen minimum role may create matching tags.")]
    public async Task<CallToolResult> ProtectTagAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Exact tag name, or a wildcard pattern such as \"v*\".")]
        string tagName,
        [Description(
            "Minimum role allowed to create matching tags, as a GitLab access level: 0=No access, 30=Developer, 40=Maintainer, 60=Admin. Omit for GitLab's default (Maintainer).")]
        int? createAccessLevel = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ProtectTagRequest { Name = tagName, CreateAccessLevel = createAccessLevel };
        var protectedTag = await protectedTags.ProtectAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(protectedTag), "projects/:id/protected_tags (create)");
    }

    [McpServerTool(Name = "gitlab_update_file", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Replaces an existing file's content and commits the change. Fails if the file does not already exist on the given branch — use gitlab_create_file for a new file.")]
    public async Task<CallToolResult> UpdateFileAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Full path from the repository root, e.g. \"src/Program.cs\".")]
        string filePath,
        [Description("The branch to commit the change to. Must already exist.")]
        string branch,
        [Description("The file's new content, replacing its previous content entirely.")]
        string content,
        [Description("The commit message.")] string commitMessage,
        [Description("Set to \"base64\" if content is Base64-encoded binary data. Omit for plain text.")]
        string? encoding = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateRepositoryFileRequest
        {
            Branch = branch,
            Content = content,
            CommitMessage = commitMessage,
            Encoding = encoding
        };

        var file = await repositoryFiles.UpdateAsync((ProjectId)project, filePath, request, cancellationToken);
        return GitLabContent.Wrap(RepositoryMapper.ToSummary(file),
            "projects/:id/repository/files/:file_path (update)");
    }

    [McpServerTool(Name = "gitlab_delete_file", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a file and commits its removal from a branch. This cannot be undone through this tool.")]
    public async Task<FileDeleteResult> DeleteFileAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Full path from the repository root of the file to delete.")]
        string filePath,
        [Description("The branch to commit the deletion to.")]
        string branch,
        [Description("The commit message.")] string commitMessage,
        CancellationToken cancellationToken = default)
    {
        await repositoryFiles.DeleteAsync((ProjectId)project, filePath, branch, commitMessage, cancellationToken);
        return new FileDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_cherry_pick_commit", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Applies an existing commit onto another branch as a new commit.")]
    public async Task<CallToolResult> CherryPickCommitAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA, or a branch/tag name, to cherry-pick.")]
        string sha,
        [Description("The branch to apply the cherry-pick onto.")]
        string branch,
        [Description("Overrides the resulting commit's message. Omit to keep the original commit's message.")]
        string? message = null,
        [Description("If true, validates the cherry-pick without actually committing it. Default false.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var request = new CherryPickCommitRequest { Branch = branch, Message = message, DryRun = dryRun };
        var commit = await commits.CherryPickAsync((ProjectId)project, sha, request, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToSummary(commit), "projects/:id/repository/commits/:sha/cherry_pick");
    }

    [McpServerTool(Name = "gitlab_revert_commit", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a new commit that undoes another commit's changes on a branch.")]
    public async Task<CallToolResult> RevertCommitAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA, or a branch/tag name, to revert.")]
        string sha,
        [Description("The branch to commit the revert onto.")]
        string branch,
        [Description("If true, validates the revert without actually committing it. Default false.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var request = new RevertCommitRequest { Branch = branch, DryRun = dryRun };
        var commit = await commits.RevertAsync((ProjectId)project, sha, request, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToSummary(commit), "projects/:id/repository/commits/:sha/revert");
    }

    [McpServerTool(Name = "gitlab_comment_on_commit", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Leaves a comment on a commit, optionally anchored to a specific line of its diff.")]
    public async Task<CallToolResult> CommentOnCommitAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description("The comment text.")] string note,
        [Description(
            "File path to anchor the comment to a diff line. Supply together with line and lineType, or omit all three for a commit-level comment.")]
        string? path = null,
        [Description("Line number within the file, paired with path and lineType.")]
        int? line = null,
        [Description(
            "Which side of the diff the line refers to: \"old\" or \"new\". Required when path and line are given.")]
        string? lineType = null,
        CancellationToken cancellationToken = default)
    {
        GitLabCommitLineType? parsedLineType = lineType?.ToLowerInvariant() switch
        {
            "old" => GitLabCommitLineType.Old,
            "new" => GitLabCommitLineType.New,
            null or "" => null,
            _ => throw new McpException($"lineType must be \"old\", \"new\", or omitted; got \"{lineType}\".")
        };

        var request = new CreateCommitCommentRequest
        {
            Note = note,
            Path = path,
            Line = line,
            LineType = parsedLineType
        };

        var comment = await commits.CreateCommentAsync((ProjectId)project, sha, request, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToSummary(comment),
            "projects/:id/repository/commits/:sha/comments (create)");
    }

    [McpServerTool(Name = "gitlab_delete_branch", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a single branch. This cannot be undone through this tool.")]
    public async Task<BranchDeleteResult> DeleteBranchAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The branch's exact name.")]
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await branches.DeleteAsync((ProjectId)project, branchName, cancellationToken);
        return new BranchDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_delete_merged_branches", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Bulk-deletes every branch already merged into the project's default branch (the default branch itself, and any protected branch, are always kept). GitLab queues the deletion, so branches are not necessarily gone the instant this returns. This cannot be undone through this tool.")]
    public async Task<MergedBranchesDeleteResult> DeleteMergedBranchesAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        CancellationToken cancellationToken = default)
    {
        await branches.DeleteMergedAsync((ProjectId)project, cancellationToken);
        return new MergedBranchesDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_delete_tag", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a tag. This cannot be undone through this tool.")]
    public async Task<TagDeleteResult> DeleteTagAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The tag's exact name.")] string tagName,
        CancellationToken cancellationToken = default)
    {
        await tags.DeleteAsync((ProjectId)project, tagName, cancellationToken);
        return new TagDeleteResult(true);
    }

    [McpServerTool(Name = "gitlab_set_commit_status", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Reports or updates a build/check status against a commit, for an external CI system integrating with GitLab. Posting again with the same name updates the existing status rather than creating a duplicate.")]
    public async Task<CallToolResult> SetCommitStatusAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The commit's full or short SHA.")]
        string sha,
        [Description(
            "The status to report: \"pending\", \"running\", \"success\", \"failed\", \"canceled\", or \"skipped\".")]
        string state,
        [Description(
            "A label distinguishing this status from other systems' statuses. GitLab defaults it to \"default\" if omitted.")]
        string? name = null,
        [Description("URL to link the status to, e.g. a CI job page. Must be an absolute URL if given.")]
        string? targetUrl = null,
        [Description("Short human-readable description of the status.")]
        string? description = null,
        [Description("Test coverage percentage (0-100) to record alongside the status. Omit if not applicable.")]
        double? coverage = null,
        [Description(
            "Restricts the status to one specific pipeline, when the project has several pipelines against the same commit. Omit to apply to the commit generally.")]
        long? pipelineId = null,
        CancellationToken cancellationToken = default)
    {
        if (state.ToLowerInvariant() is not ("pending" or "running" or "success" or "failed" or "canceled"
            or "skipped"))
            throw new McpException(
                $"state must be one of \"pending\", \"running\", \"success\", \"failed\", \"canceled\", \"skipped\"; got \"{state}\".");

        var request = new CreateCommitStatusRequest
        {
            State = state,
            Name = name,
            TargetUrl = ParseOptionalUrl(targetUrl),
            Description = description,
            Coverage = coverage,
            PipelineId = pipelineId
        };

        var status = await commitStatuses.CreateAsync((ProjectId)project, sha, request, cancellationToken);
        return GitLabContent.Wrap(CommitMapper.ToSummary(status), "projects/:id/statuses/:sha (create)");
    }

    [McpServerTool(Name = "gitlab_unprotect_branch", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes protection from a branch or wildcard pattern, restoring GitLab's default (unrestricted) push/merge rules for it.")]
    public async Task<BranchUnprotectResult> UnprotectBranchAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The exact protected branch name or wildcard pattern to unprotect, as it appears in gitlab_list_protected_branches.")]
        string branchName,
        CancellationToken cancellationToken = default)
    {
        await protectedBranches.UnprotectAsync((ProjectId)project, branchName, cancellationToken);
        return new BranchUnprotectResult(true);
    }

    [McpServerTool(Name = "gitlab_unprotect_tag", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes protection from a tag or wildcard pattern, restoring GitLab's default (unrestricted) tag-creation rules for it.")]
    public async Task<TagUnprotectResult> UnprotectTagAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description(
            "The exact protected tag name or wildcard pattern to unprotect, as it appears in gitlab_list_protected_tags.")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        await protectedTags.UnprotectAsync((ProjectId)project, tagName, cancellationToken);
        return new TagUnprotectResult(true);
    }

    [McpServerTool(Name = "gitlab_create_push_rule", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a project's first push rule: server-side checks GitLab runs against every push (commit message shape, branch naming, secret detection, signing). Fails if the project already has a push rule configured — use gitlab_update_push_rule instead.")]
    public async Task<CallToolResult> CreatePushRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Regex a commit message must match for the push to be accepted. Omit for no requirement.")]
        string? commitMessageRegex = null,
        [Description("Regex a commit message must NOT match for the push to be accepted. Omit for no requirement.")]
        string? commitMessageNegativeRegex = null,
        [Description("Regex a branch name must match to accept a push that creates it. Omit for no requirement.")]
        string? branchNameRegex = null,
        [Description("If true, rejects any push that deletes a tag. Omit to use GitLab's default.")]
        bool? denyDeleteTag = null,
        [Description(
            "If true, restricts who may push to committers who are already project members. Omit to use GitLab's default.")]
        bool? memberCheck = null,
        [Description(
            "If true, rejects pushes containing files GitLab's secret-detection recognizes as credentials. Omit to use GitLab's default.")]
        bool? preventSecrets = null,
        [Description("Regex a commit's author email must match to be accepted. Omit for no requirement.")]
        string? authorEmailRegex = null,
        [Description("Regex forbidding matching file names or paths from being pushed. Omit for no requirement.")]
        string? fileNameRegex = null,
        [Description("Maximum size, in megabytes, of any single file a push may add. Omit for no limit.")]
        int? maxFileSize = null,
        [Description(
            "If true, requires the commit's committer to already be a GitLab user. Omit to use GitLab's default.")]
        bool? commitCommitterCheck = null,
        [Description(
            "If true, requires the commit's committer name to match the committing GitLab user's name. Omit to use GitLab's default.")]
        bool? commitCommitterNameCheck = null,
        [Description("If true, rejects commits that are not cryptographically signed. Omit to use GitLab's default.")]
        bool? rejectUnsignedCommits = null,
        [Description(
            "If true, rejects commits missing a Developer Certificate of Origin sign-off. Omit to use GitLab's default.")]
        bool? rejectNonDcoCommits = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePushRuleRequest
        {
            CommitMessageRegex = commitMessageRegex,
            CommitMessageNegativeRegex = commitMessageNegativeRegex,
            BranchNameRegex = branchNameRegex,
            DenyDeleteTag = denyDeleteTag,
            MemberCheck = memberCheck,
            PreventSecrets = preventSecrets,
            AuthorEmailRegex = authorEmailRegex,
            FileNameRegex = fileNameRegex,
            MaxFileSize = maxFileSize,
            CommitCommitterCheck = commitCommitterCheck,
            CommitCommitterNameCheck = commitCommitterNameCheck,
            RejectUnsignedCommits = rejectUnsignedCommits,
            RejectNonDcoCommits = rejectNonDcoCommits
        };

        var rule = await pushRules.CreateForProjectAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(rule), "projects/:id/push_rule (create)");
    }

    [McpServerTool(Name = "gitlab_update_push_rule", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes an existing project push rule. Every parameter is optional — an omitted one leaves that check untouched rather than clearing it. Fails if the project has no push rule configured yet — use gitlab_create_push_rule first.")]
    public async Task<CallToolResult> UpdatePushRuleAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("Regex a commit message must match for the push to be accepted. Omit to leave unchanged.")]
        string? commitMessageRegex = null,
        [Description("Regex a commit message must NOT match for the push to be accepted. Omit to leave unchanged.")]
        string? commitMessageNegativeRegex = null,
        [Description("Regex a branch name must match to accept a push that creates it. Omit to leave unchanged.")]
        string? branchNameRegex = null,
        [Description("If true, rejects any push that deletes a tag. Omit to leave unchanged.")]
        bool? denyDeleteTag = null,
        [Description(
            "If true, restricts who may push to committers who are already project members. Omit to leave unchanged.")]
        bool? memberCheck = null,
        [Description(
            "If true, rejects pushes containing files GitLab's secret-detection recognizes as credentials. Omit to leave unchanged.")]
        bool? preventSecrets = null,
        [Description("Regex a commit's author email must match to be accepted. Omit to leave unchanged.")]
        string? authorEmailRegex = null,
        [Description("Regex forbidding matching file names or paths from being pushed. Omit to leave unchanged.")]
        string? fileNameRegex = null,
        [Description("Maximum size, in megabytes, of any single file a push may add. Omit to leave unchanged.")]
        int? maxFileSize = null,
        [Description("If true, requires the commit's committer to already be a GitLab user. Omit to leave unchanged.")]
        bool? commitCommitterCheck = null,
        [Description(
            "If true, requires the commit's committer name to match the committing GitLab user's name. Omit to leave unchanged.")]
        bool? commitCommitterNameCheck = null,
        [Description("If true, rejects commits that are not cryptographically signed. Omit to leave unchanged.")]
        bool? rejectUnsignedCommits = null,
        [Description(
            "If true, rejects commits missing a Developer Certificate of Origin sign-off. Omit to leave unchanged.")]
        bool? rejectNonDcoCommits = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePushRuleRequest
        {
            CommitMessageRegex = commitMessageRegex,
            CommitMessageNegativeRegex = commitMessageNegativeRegex,
            BranchNameRegex = branchNameRegex,
            DenyDeleteTag = denyDeleteTag,
            MemberCheck = memberCheck,
            PreventSecrets = preventSecrets,
            AuthorEmailRegex = authorEmailRegex,
            FileNameRegex = fileNameRegex,
            MaxFileSize = maxFileSize,
            CommitCommitterCheck = commitCommitterCheck,
            CommitCommitterNameCheck = commitCommitterNameCheck,
            RejectUnsignedCommits = rejectUnsignedCommits,
            RejectNonDcoCommits = rejectNonDcoCommits
        };

        var rule = await pushRules.UpdateForProjectAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(ProtectionMapper.ToSummary(rule), "projects/:id/push_rule (update)");
    }

    [McpServerTool(Name = "gitlab_create_push_mirror", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Configures a new push mirror: GitLab will push every update on this project to the given remote. The remote URL may embed its own credentials (e.g. \"https://user:token@host/repo.git\"); GitLab stores them but never returns them again — later reads show the URL with credentials scrubbed.")]
    public async Task<CallToolResult> CreatePushMirrorAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The remote git URL to push to, including any embedded auth credentials the remote requires.")]
        string url,
        [Description("If true, the mirror starts enabled and syncs automatically. Default true.")]
        bool enabled = true,
        [Description(
            "How the remote authenticates the push: \"ssh_public_key\" or \"password\". Omit to let GitLab infer it from the URL.")]
        string? authMethod = null,
        [Description(
            "If true, keeps refs on the remote that have diverged instead of overwriting them on the next sync. Omit to use GitLab's default.")]
        bool? keepDivergentRefs = null,
        [Description("If true, mirrors only protected branches. Mutually exclusive with mirrorBranchRegex.")]
        bool? onlyProtectedBranches = null,
        [Description(
            "Only branches whose name matches this regex are mirrored. Mutually exclusive with onlyProtectedBranches.")]
        string? mirrorBranchRegex = null,
        [Description(
            "SSH host keys to trust for the remote, each in bare (\"ssh-ed25519 AAAA...\") or full known_hosts format. Omit if the remote does not need one pinned.")]
        IReadOnlyList<string>? hostKeys = null,
        CancellationToken cancellationToken = default)
    {
        if (onlyProtectedBranches == true && !string.IsNullOrEmpty(mirrorBranchRegex))
            throw new McpException(
                "onlyProtectedBranches and mirrorBranchRegex are mutually exclusive; set at most one.");

        if (authMethod is not null && authMethod is not ("ssh_public_key" or "password"))
            throw new McpException(
                $"authMethod must be \"ssh_public_key\", \"password\", or omitted; got \"{authMethod}\".");

        var request = new CreateRemoteMirrorRequest
        {
            Url = url,
            Enabled = enabled,
            AuthMethod = authMethod,
            KeepDivergentRefs = keepDivergentRefs,
            OnlyProtectedBranches = onlyProtectedBranches,
            MirrorBranchRegex = mirrorBranchRegex,
            HostKeys = hostKeys
        };

        var mirror = await remoteMirrors.CreateAsync((ProjectId)project, request, cancellationToken);
        return GitLabContent.Wrap(RemoteMirrorMapper.ToSummary(mirror), "projects/:id/remote_mirrors (create)");
    }

    // ---------------------------------------------------------------------------------------------

    private static Uri? ParseOptionalUrl(string? url)
    {
        if (url is null) return null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return parsed;

        throw new McpException("targetUrl must be an absolute URL, e.g. \"https://ci.example.com/build/42\".");
    }

    private static CommitAction ToCommitAction(CommitFileAction action)
    {
        var actionType = action.Action.ToLowerInvariant() switch
        {
            "create" => GitLabCommitActionType.Create,
            "update" => GitLabCommitActionType.Update,
            "delete" => GitLabCommitActionType.Delete,
            "move" => GitLabCommitActionType.Move,
            "chmod" => GitLabCommitActionType.Chmod,
            _ => throw new McpException(
                $"Unrecognized action \"{action.Action}\" — must be one of: create, update, delete, move, chmod.")
        };

        if (actionType == GitLabCommitActionType.Move && string.IsNullOrEmpty(action.PreviousPath))
            throw new McpException("previousPath is required when action is \"move\".");

        GitLabCommitActionEncoding? encoding = action.Encoding?.ToLowerInvariant() switch
        {
            "text" => GitLabCommitActionEncoding.Text,
            "base64" => GitLabCommitActionEncoding.Base64,
            null or "" => null,
            _ => throw new McpException(
                $"Unrecognized encoding \"{action.Encoding}\" — must be \"text\", \"base64\", or omitted.")
        };

        return new CommitAction
        {
            Action = actionType,
            FilePath = action.FilePath,
            PreviousPath = action.PreviousPath,
            Content = action.Content,
            Encoding = encoding,
            ExecuteFilemode = action.ExecuteFilemode
        };
    }
}