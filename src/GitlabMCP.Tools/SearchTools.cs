using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using GitLab.Client.Abstractions;
using GitLab.Client.Domain;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Search;
using GitlabMCP.Mapping.Search;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Search, analytics, wiki, snippet and template tools (domain "search"), plus the instance-admin Zoekt
///     code-search index, Knowledge Graph and ActiveContext collection surfaces that live on the same
///     resource clients. Most tools here return GitLab-authored text (snippet/template/wiki/analytics
///     content) and wrap via <see cref="GitLabContent" />; the Zoekt/Knowledge-Graph rows are the rare
///     all-numeric-id exception and declare a bare record instead (DEC-007).
/// </summary>
[McpServerToolType]
public sealed class SearchTools(
    ISearchClient search,
    ISnippetsClient snippets,
    ITemplatesClient templates,
    IAnalyticsClient analytics,
    IWikisClient wikis,
    ICodeSearchClient codeSearch,
    IKnowledgeGraphClient knowledgeGraph,
    IActiveContextClient activeContext)
{
    private const int MaxLimit = 100;
    private const int MaxRawTextLength = 20_000;

    // ---------------------------------------------------------------------------------------------
    // Reads
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_search_migrations", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every advanced-search (Elasticsearch) migration known to the instance, for diagnosing search-index health. Requires an administrator token; returns HTTP 403 for any other token. The response shape is instance-defined, so it is returned as raw JSON text rather than a fixed schema.")]
    public async Task<CallToolResult> ListSearchMigrationsAsync(CancellationToken cancellationToken)
    {
        var migrations = await search.ListSearchMigrationsAsync(cancellationToken);
        var raw = migrations.GetRawText();
        var text = raw.Length > MaxRawTextLength
            ? raw[..MaxRawTextLength] + "\n... (truncated)"
            : raw;

        return GitLabContent.WrapText(text, "admin/search/migrations");
    }

    [McpServerTool(Name = "gitlab_list_all_snippets", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every snippet on the instance, personal and project alike. Requires Administrator or Auditor access; any other token sees only what it could already see through the ordinary snippet endpoints.")]
    public async Task<CallToolResult> ListAllSnippetsAsync(
        [Description(
            "Only return snippets created at or after this UTC timestamp, ISO 8601 (e.g. \"2025-01-01T00:00:00Z\"). Omit for no lower bound.")]
        DateTimeOffset? createdAfter = null,
        [Description(
            "Only return snippets created at or before this UTC timestamp, ISO 8601. Omit for no upper bound.")]
        DateTimeOffset? createdBefore = null,
        [Description("Maximum snippets to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new AllSnippetListOptions
        {
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<SnippetSummary> collected = [];
        var truncated = false;

        await foreach (var snippet in snippets.ListAllAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SnippetMapper.ToSummary(snippet));
        }

        return GitLabContent.Wrap(new SnippetListResult(collected, truncated), "snippets/all");
    }

    [McpServerTool(Name = "gitlab_list_license_templates", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists available open-source license templates with metadata (key, name, popular flag, links). Does not include the full license text — the returned htmlUrl points at GitLab's rendering of it.")]
    public async Task<CallToolResult> ListLicenseTemplatesAsync(
        [Description(
            "If true, returns only the short list of commonly used licenses (MIT, Apache-2.0, GPL, ...). If false or omitted, returns every license GitLab knows.")]
        bool popularOnly = false,
        [Description("Maximum templates to return (1-100). Default 50 — the full catalog is under 100.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new LicenseTemplateListOptions
        {
            Popular = popularOnly ? true : null,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<LicenseTemplateSummary> collected = [];
        var truncated = false;

        await foreach (var template in templates.ListLicensesAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(TemplateMapper.ToSummary(template));
        }

        return GitLabContent.Wrap(new LicenseTemplateListResult(collected, truncated), "templates/licenses");
    }

    [McpServerTool(Name = "gitlab_list_code_review_analytics", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Streams code-review metrics (review time, approvers, diff stats) for a project's open merge requests, for spotting stalled or slow-reviewed MRs.")]
    public async Task<CallToolResult> ListCodeReviewAnalyticsAsync(
        [Description(
            "The project's numeric id. This analytics endpoint only accepts the numeric id, not the \"namespace/path\" form.")]
        long projectId,
        [Description(
            "Comma-separated label names; only merge requests carrying every one of them are included. Omit for no label filter.")]
        string? labelNames = null,
        [Description("Only include merge requests targeting this milestone title. Omit for no milestone filter.")]
        string? milestoneTitle = null,
        [Description("Maximum items to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new CodeReviewAnalyticsListOptions
        {
            LabelName = ParseStringList(labelNames),
            MilestoneTitle = milestoneTitle,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<CodeReviewAnalyticsItemSummary> collected = [];
        var truncated = false;

        await foreach (var item in analytics.ListCodeReviewAnalyticsAsync(projectId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AnalyticsMapper.ToSummary(item));
        }

        return GitLabContent.Wrap(new CodeReviewAnalyticsListResult(collected, truncated),
            "projects/:id/analytics/code_review_analytics");
    }

    [McpServerTool(Name = "gitlab_list_wiki_pages", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's or group's wiki pages (title/slug/format). Give exactly one of project or group. Group wikis require GitLab Premium or Ultimate.")]
    public async Task<CallToolResult> ListWikiPagesAsync(
        [Description(
            "Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\"). Omit if group is given instead.")]
        string? project = null,
        [Description(
            "Group: numeric group id or URL-encoded path (\"my-group\" or \"my-group/subgroup\"). Omit if project is given instead.")]
        string? group = null,
        [Description(
            "If true, each returned page includes its markdown content, not just title/slug/format. Content is truncated at 8000 characters per page (see contentTruncated on the result). Default false.")]
        bool withContent = false,
        [Description("Maximum pages to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        List<WikiPageSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            await foreach (var page in wikis.ListForProjectAsync((ProjectId)project!, withContent, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(WikiMapper.ToSummary(page));
            }

            return GitLabContent.Wrap(new WikiPageListResult(collected, truncated), "projects/:id/wikis");
        }

        await foreach (var page in wikis.ListForGroupAsync((GroupId)group!, withContent, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(WikiMapper.ToSummary(page));
        }

        return GitLabContent.Wrap(new WikiPageListResult(collected, truncated), "groups/:id/wikis");
    }

    [McpServerTool(Name = "gitlab_list_code_search_indexed_namespaces", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every namespace indexed on one Zoekt exact-code-search node. Instance-administrator access required; GitLab returns 403 for any other token.")]
    public async Task<ZoektIndexedNamespaceListResult> ListCodeSearchIndexedNamespacesAsync(
        [Description("The Zoekt node's numeric id, from the instance's admin/Zoekt shard listing.")]
        long nodeId,
        [Description("Maximum namespaces to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var all = await codeSearch.ListIndexedNamespacesAsync(nodeId, cancellationToken);
        var truncated = all.Count > limit;
        var collected = all.Take(limit).Select(CodeSearchMapper.ToSummary).ToList();

        return new ZoektIndexedNamespaceListResult(collected, truncated);
    }

    [McpServerTool(Name = "gitlab_list_knowledge_graph_namespaces", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every namespace with GitLab Duo Knowledge Graph enabled on this instance. Instance-administrator access required; GitLab returns 403 for any other token.")]
    public async Task<KnowledgeGraphNamespaceListResult> ListKnowledgeGraphNamespacesAsync(
        [Description("Maximum namespaces to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var all = await knowledgeGraph.ListNamespacesAsync(cancellationToken);
        var truncated = all.Count > limit;
        var collected = all.Take(limit).Select(KnowledgeGraphMapper.ToSummary).ToList();

        return new KnowledgeGraphNamespaceListResult(collected, truncated);
    }

    [McpServerTool(Name = "gitlab_search_projects", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches for projects by name, description or path. Searches the whole instance by default; give group to narrow the search to one group's projects, including its subgroups.")]
    public async Task<CallToolResult> SearchProjectsAsync(
        [Description("The search text to match against project name, description and path.")]
        string query,
        [Description(
            "Restrict the search to this group's projects: numeric group id or URL-encoded path (\"my-group\" or \"my-group/subgroup\"). Omit to search the whole instance.")]
        string? group = null,
        [Description("Maximum projects to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<SearchProjectSummary> collected = [];
        var truncated = false;

        if (!string.IsNullOrEmpty(group))
        {
            await foreach (var project in search.SearchGroupProjectsAsync((GroupId)group, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(project));
            }

            return GitLabContent.Wrap(new SearchProjectListResult(collected, truncated),
                "groups/:id/search (scope=projects)");
        }

        await foreach (var project in search.SearchProjectsAsync(query, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(project));
        }

        return GitLabContent.Wrap(new SearchProjectListResult(collected, truncated), "search (scope=projects)");
    }

    [McpServerTool(Name = "gitlab_search_issues", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Full-text search for issues by title and description. Searches the whole instance by default; give at most one of project or group to narrow the search to one project or one group (including its subgroups).")]
    public async Task<CallToolResult> SearchIssuesAsync(
        [Description("The search text to match against issue title and description.")]
        string query,
        [Description(
            "Restrict the search to this project: numeric project id or URL-encoded \"namespace/path\". Give at most one of project or group; omit both for an instance-wide search.")]
        string? project = null,
        [Description(
            "Restrict the search to this group (including its subgroups): numeric group id or URL-encoded path. Give at most one of project or group; omit both for an instance-wide search.")]
        string? group = null,
        [Description("Filter by state: \"all\", \"opened\", \"closed\", \"merged\", or omit for GitLab's default.")]
        string? state = null,
        [Description("Maximum issues to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (state is not (null or "" or "all" or "opened" or "closed" or "merged"))
            throw new McpException("state must be \"all\", \"opened\", \"closed\", \"merged\", or omitted.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject && hasGroup) throw new McpException("Give at most one of project or group.");

        List<SearchIssueSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var issue in search.SearchProjectIssuesAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(issue));
            }

            return GitLabContent.Wrap(new SearchIssueListResult(collected, truncated),
                "projects/:id/search (scope=issues)");
        }

        if (hasGroup)
        {
            var options = new SearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var issue in search.SearchGroupIssuesAsync((GroupId)group!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(issue));
            }

            return GitLabContent.Wrap(new SearchIssueListResult(collected, truncated),
                "groups/:id/search (scope=issues)");
        }

        var instanceOptions = new SearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var issue in search.SearchIssuesAsync(query, instanceOptions, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(issue));
        }

        return GitLabContent.Wrap(new SearchIssueListResult(collected, truncated), "search (scope=issues)");
    }

    [McpServerTool(Name = "gitlab_search_merge_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Full-text search for merge requests by title and description. Searches the whole instance by default; give at most one of project or group to narrow the search to one project or one group (including its subgroups).")]
    public async Task<CallToolResult> SearchMergeRequestsAsync(
        [Description("The search text to match against merge request title and description.")]
        string query,
        [Description(
            "Restrict the search to this project: numeric project id or URL-encoded \"namespace/path\". Give at most one of project or group; omit both for an instance-wide search.")]
        string? project = null,
        [Description(
            "Restrict the search to this group (including its subgroups): numeric group id or URL-encoded path. Give at most one of project or group; omit both for an instance-wide search.")]
        string? group = null,
        [Description("Filter by state: \"all\", \"opened\", \"closed\", \"merged\", or omit for GitLab's default.")]
        string? state = null,
        [Description("Maximum merge requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (state is not (null or "" or "all" or "opened" or "closed" or "merged"))
            throw new McpException("state must be \"all\", \"opened\", \"closed\", \"merged\", or omitted.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject && hasGroup) throw new McpException("Give at most one of project or group.");

        List<SearchMergeRequestSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var mr in search.SearchProjectMergeRequestsAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(mr));
            }

            return GitLabContent.Wrap(new SearchMergeRequestListResult(collected, truncated),
                "projects/:id/search (scope=merge_requests)");
        }

        if (hasGroup)
        {
            var options = new SearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var mr in search.SearchGroupMergeRequestsAsync((GroupId)group!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(mr));
            }

            return GitLabContent.Wrap(new SearchMergeRequestListResult(collected, truncated),
                "groups/:id/search (scope=merge_requests)");
        }

        var instanceOptions = new SearchListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var mr in search.SearchMergeRequestsAsync(query, instanceOptions, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(mr));
        }

        return GitLabContent.Wrap(new SearchMergeRequestListResult(collected, truncated),
            "search (scope=merge_requests)");
    }

    [McpServerTool(Name = "gitlab_search_users", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Finds users by name, username or public email. Searches the whole instance by default; give at most one of project or group to narrow the search to one project's or one group's members.")]
    public async Task<CallToolResult> SearchUsersAsync(
        [Description("The search text to match against username, name and public email.")]
        string query,
        [Description(
            "Restrict the search to this project's members: numeric project id or URL-encoded \"namespace/path\". Give at most one of project or group; omit both for an instance-wide search.")]
        string? project = null,
        [Description(
            "Restrict the search to this group's members: numeric group id or URL-encoded path. Give at most one of project or group; omit both for an instance-wide search.")]
        string? group = null,
        [Description("Maximum users to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject && hasGroup) throw new McpException("Give at most one of project or group.");

        List<SearchUserSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var user in search.SearchProjectUsersAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(user));
            }

            return GitLabContent.Wrap(new SearchUserListResult(collected, truncated),
                "projects/:id/search (scope=users)");
        }

        if (hasGroup)
        {
            var options = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var user in search.SearchGroupUsersAsync((GroupId)group!, query, options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(user));
            }

            return GitLabContent.Wrap(new SearchUserListResult(collected, truncated),
                "groups/:id/search (scope=users)");
        }

        var instanceOptions = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var user in search.SearchUsersAsync(query, instanceOptions, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(user));
        }

        return GitLabContent.Wrap(new SearchUserListResult(collected, truncated), "search (scope=users)");
    }

    [McpServerTool(Name = "gitlab_search_milestones", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches milestone titles and descriptions within one group or one project. GitLab has no instance-wide milestone search, so give exactly one of project or group.")]
    public async Task<CallToolResult> SearchMilestonesAsync(
        [Description("The search text to match against milestone title and description.")]
        string query,
        [Description(
            "Search this project's milestones: numeric project id or URL-encoded \"namespace/path\". Give exactly one of project or group.")]
        string? project = null,
        [Description(
            "Search this group's milestones (including its subgroups): numeric group id or URL-encoded path. Give exactly one of project or group.")]
        string? group = null,
        [Description("Maximum milestones to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        List<SearchMilestoneSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var milestone in search.SearchProjectMilestonesAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(milestone));
            }

            return GitLabContent.Wrap(new SearchMilestoneListResult(collected, truncated),
                "projects/:id/search (scope=milestones)");
        }

        var groupOptions = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var milestone in search.SearchGroupMilestonesAsync((GroupId)group!, query, groupOptions,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(milestone));
        }

        return GitLabContent.Wrap(new SearchMilestoneListResult(collected, truncated),
            "groups/:id/search (scope=milestones)");
    }

    [McpServerTool(Name = "gitlab_search_notes", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches comment text within one group or one project. GitLab has no instance-wide note search, so give exactly one of project or group.")]
    public async Task<CallToolResult> SearchNotesAsync(
        [Description("The search text to match against comment bodies.")]
        string query,
        [Description(
            "Search this project's notes: numeric project id or URL-encoded \"namespace/path\". Give exactly one of project or group.")]
        string? project = null,
        [Description(
            "Search this group's notes (including its subgroups): numeric group id or URL-encoded path. Give exactly one of project or group.")]
        string? group = null,
        [Description("Maximum notes to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        List<SearchNoteSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var note in search.SearchProjectNotesAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(note));
            }

            return GitLabContent.Wrap(new SearchNoteListResult(collected, truncated),
                "projects/:id/search (scope=notes)");
        }

        var groupOptions = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var note in
                       search.SearchGroupNotesAsync((GroupId)group!, query, groupOptions, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(note));
        }

        return GitLabContent.Wrap(new SearchNoteListResult(collected, truncated), "groups/:id/search (scope=notes)");
    }

    [McpServerTool(Name = "gitlab_search_commits", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches commit messages within one group or one project. GitLab has no instance-wide commit search, so give exactly one of project or group.")]
    public async Task<CallToolResult> SearchCommitsAsync(
        [Description("The search text to match against commit messages.")]
        string query,
        [Description(
            "Search this project's commits: numeric project id or URL-encoded \"namespace/path\". Give exactly one of project or group.")]
        string? project = null,
        [Description(
            "Search this group's commits (including its subgroups): numeric group id or URL-encoded path. Give exactly one of project or group.")]
        string? group = null,
        [Description(
            "Only search commits reachable from this branch or tag. Defaults to the project's default branch. Only valid when project is given, not group.")]
        string? refName = null,
        [Description("Maximum commits to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        if (hasGroup && !string.IsNullOrEmpty(refName))
            throw new McpException("refName is only valid together with project, not group.");

        List<SearchCommitSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            var options = new ProjectSearchListOptions { Ref = refName, PerPage = Math.Min(limit + 1, MaxLimit) };

            await foreach (var commit in search.SearchProjectCommitsAsync((ProjectId)project!, query, options,
                               cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SearchResultMapper.ToSummary(commit));
            }

            return GitLabContent.Wrap(new SearchCommitListResult(collected, truncated),
                "projects/:id/search (scope=commits)");
        }

        var groupOptions = new SearchListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        await foreach (var commit in search.SearchGroupCommitsAsync((GroupId)group!, query, groupOptions,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SearchResultMapper.ToSummary(commit));
        }

        return GitLabContent.Wrap(new SearchCommitListResult(collected, truncated),
            "groups/:id/search (scope=commits)");
    }

    [McpServerTool(Name = "gitlab_search_semantic_code", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches one project's indexed repository by meaning rather than keyword (e.g. \"where do we validate a webhook signature\"). Requires GitLab semantic code search to be enabled and indexed for the project. Returns one ranked result set, not a paginated list.")]
    public async Task<CallToolResult> SearchSemanticCodeAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("A natural-language description of the code you're looking for.")]
        string query,
        [Description(
            "Restrict the search to files under this repository directory (e.g. \"src/api\"). Omit to search the whole repository.")]
        string? directoryPath = null,
        [Description(
            "Number of nearest-neighbour candidates the underlying vector search considers before ranking. Omit for GitLab's default.")]
        int? knn = null,
        [Description("Maximum matches to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new SemanticCodeSearchOptions
        {
            DirectoryPath = directoryPath,
            Knn = knn,
            Limit = limit
        };

        var result = await search.SearchProjectSemanticCodeAsync((ProjectId)project, query, options, cancellationToken);
        var allMatches = result.Results ?? [];
        var truncated = allMatches.Count > limit;
        var matches = allMatches.Take(limit).Select(SearchResultMapper.ToSummary).ToList();

        return GitLabContent.Wrap(
            new SemanticCodeSearchSummary(result.Confidence, matches, truncated),
            "projects/:id/search/semantic");
    }

    [McpServerTool(Name = "gitlab_get_search_migration", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one advanced-search (Elasticsearch) migration's status by version or name, for diagnosing search-index health. Requires an administrator token; returns HTTP 403 for any other token.")]
    public async Task<CallToolResult> GetSearchMigrationAsync(
        [Description(
            "The migration's version number or its name, exactly as reported by gitlab_list_search_migrations.")]
        string migrationId,
        CancellationToken cancellationToken)
    {
        var migration = await search.GetSearchMigrationAsync(migrationId, cancellationToken);
        return GitLabContent.Wrap(SearchResultMapper.ToSummary(migration), "admin/search/migrations/:migration_id");
    }

    [McpServerTool(Name = "gitlab_get_wiki_page", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads one wiki page's content by slug, optionally at a historical revision, from a project's or group's wiki. Give exactly one of project or group. Group wikis require GitLab Premium or Ultimate.")]
    public async Task<CallToolResult> GetWikiPageAsync(
        [Description(
            "The page's slug, exactly as reported by gitlab_list_wiki_pages (nested pages use \"home/setup\" form).")]
        string slug,
        [Description("Project: numeric project id or URL-encoded path. Omit if group is given instead.")]
        string? project = null,
        [Description("Group: numeric group id or URL-encoded path. Omit if project is given instead.")]
        string? group = null,
        [Description(
            "A commit sha to read a historical revision of the page instead of the current one. Omit for the current version.")]
        string? version = null,
        [Description(
            "If true, GitLab renders the content to HTML before returning it. Default false (raw markdown/format source).")]
        bool renderHtml = false,
        CancellationToken cancellationToken = default)
    {
        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        if (hasProject)
        {
            var page = await wikis.GetForProjectAsync((ProjectId)project!, slug, version, renderHtml,
                cancellationToken);
            return GitLabContent.Wrap(WikiMapper.ToSummary(page), "projects/:id/wikis/:slug");
        }

        var groupPage = await wikis.GetForGroupAsync((GroupId)group!, slug, version, renderHtml, cancellationToken);
        return GitLabContent.Wrap(WikiMapper.ToSummary(groupPage), "groups/:id/wikis/:slug");
    }

    [McpServerTool(Name = "gitlab_list_snippets", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the caller's own personal snippets, or every snippet on one project when project is given.")]
    public async Task<CallToolResult> ListSnippetsAsync(
        [Description(
            "Project: numeric project id or URL-encoded path, to list that project's snippets instead of the caller's personal ones. Omit for personal snippets.")]
        string? project = null,
        [Description(
            "Personal snippets only: only return snippets created at or after this UTC timestamp, ISO 8601. Omit for no lower bound. Not valid together with project.")]
        DateTimeOffset? createdAfter = null,
        [Description(
            "Personal snippets only: only return snippets created at or before this UTC timestamp, ISO 8601. Omit for no upper bound. Not valid together with project.")]
        DateTimeOffset? createdBefore = null,
        [Description("Maximum snippets to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var hasProject = !string.IsNullOrEmpty(project);
        if (hasProject && (createdAfter is not null || createdBefore is not null))
            throw new McpException(
                "createdAfter and createdBefore are only valid for personal snippets, not together with project.");

        List<SnippetSummary> collected = [];
        var truncated = false;

        if (hasProject)
        {
            await foreach (var snippet in snippets.ListForProjectAsync((ProjectId)project!, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(SnippetMapper.ToSummary(snippet));
            }

            return GitLabContent.Wrap(new SnippetListResult(collected, truncated), "projects/:id/snippets");
        }

        var options = new SnippetListOptions
        {
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        await foreach (var snippet in snippets.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SnippetMapper.ToSummary(snippet));
        }

        return GitLabContent.Wrap(new SnippetListResult(collected, truncated), "snippets");
    }

    [McpServerTool(Name = "gitlab_list_public_snippets", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every public snippet visible to the caller across the instance, for discovering shared code examples.")]
    public async Task<CallToolResult> ListPublicSnippetsAsync(
        [Description("Only return snippets created at or after this UTC timestamp, ISO 8601. Omit for no lower bound.")]
        DateTimeOffset? createdAfter = null,
        [Description(
            "Only return snippets created at or before this UTC timestamp, ISO 8601. Omit for no upper bound.")]
        DateTimeOffset? createdBefore = null,
        [Description("Maximum snippets to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new SnippetListOptions
        {
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<SnippetSummary> collected = [];
        var truncated = false;

        await foreach (var snippet in snippets.ListPublicAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SnippetMapper.ToSummary(snippet));
        }

        return GitLabContent.Wrap(new SnippetListResult(collected, truncated), "snippets/public");
    }

    [McpServerTool(Name = "gitlab_get_snippet", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one snippet's metadata and file list by id — a personal snippet, or (with project given) a project snippet.")]
    public async Task<CallToolResult> GetSnippetAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var snippet = string.IsNullOrEmpty(project)
            ? await snippets.GetAsync(snippetId, cancellationToken)
            : await snippets.GetForProjectAsync((ProjectId)project, snippetId, cancellationToken);

        return GitLabContent.Wrap(
            SnippetMapper.ToSummary(snippet),
            string.IsNullOrEmpty(project) ? "snippets/:id" : "projects/:id/snippets/:snippet_id");
    }

    [McpServerTool(Name = "gitlab_get_snippet_content", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads a single-file snippet's raw text content -- a personal snippet, or (with project given) a project snippet. Truncated at 20000 characters; the result says so when it does. For a multi-file snippet, use gitlab_get_snippet_file_content instead.")]
    public async Task<CallToolResult> GetSnippetContentAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        await using var file = string.IsNullOrEmpty(project)
            ? await snippets.GetRawAsync(snippetId, cancellationToken)
            : await snippets.GetRawForProjectAsync((ProjectId)project, snippetId, cancellationToken);

        using var reader = new StreamReader(file.Content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var truncated = text.Length > MaxRawTextLength;
        var body = truncated
            ? $"[content truncated -- showing the first {MaxRawTextLength} of {text.Length} characters]{Environment.NewLine}{text[..MaxRawTextLength]}"
            : text;

        return GitLabContent.WrapText(body,
            string.IsNullOrEmpty(project) ? "snippets/:id/raw" : "projects/:id/snippets/:snippet_id/raw");
    }

    [McpServerTool(Name = "gitlab_get_snippet_file_content", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads one named file's raw text content from a multi-file snippet at a given ref -- a personal snippet, or (with project given) a project snippet. Truncated at 20000 characters; the result says so when it does.")]
    public async Task<CallToolResult> GetSnippetFileContentAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description("A branch, tag or commit in the snippet's repository, e.g. \"main\".")]
        string refName,
        [Description(
            "The file's path inside the snippet, exactly as reported by gitlab_get_snippet's file list, e.g. \"src/App.cs\".")]
        string filePath,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        await using var file = string.IsNullOrEmpty(project)
            ? await snippets.GetRawFileAsync(snippetId, refName, filePath, cancellationToken)
            : await snippets.GetRawFileForProjectAsync((ProjectId)project, snippetId, refName, filePath,
                cancellationToken);

        using var reader = new StreamReader(file.Content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var truncated = text.Length > MaxRawTextLength;
        var body = truncated
            ? $"[content truncated -- showing the first {MaxRawTextLength} of {text.Length} characters]{Environment.NewLine}{text[..MaxRawTextLength]}"
            : text;

        return GitLabContent.WrapText(
            body,
            string.IsNullOrEmpty(project)
                ? "snippets/:id/files/:ref/:file_path/raw"
                : "projects/:id/snippets/:snippet_id/files/:ref/:file_path/raw");
    }

    [McpServerTool(Name = "gitlab_get_snippet_user_agent_detail", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets the user-agent, IP address and spam-check details GitLab recorded against a snippet, for abuse investigation -- a personal snippet, or (with project given) a project snippet. Requires administrator access; a non-admin token gets \"not found\" rather than \"forbidden\".")]
    public async Task<CallToolResult> GetSnippetUserAgentDetailAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var detail = string.IsNullOrEmpty(project)
            ? await snippets.GetUserAgentDetailAsync(snippetId, cancellationToken)
            : await snippets.GetUserAgentDetailForProjectAsync((ProjectId)project, snippetId, cancellationToken);

        return GitLabContent.Wrap(
            SnippetMapper.ToUserAgentDetail(snippetId, detail),
            string.IsNullOrEmpty(project)
                ? "snippets/:id/user_agent_detail"
                : "projects/:id/snippets/:snippet_id/user_agent_detail");
    }

    [McpServerTool(Name = "gitlab_list_templates", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the instance's built-in scaffolding templates by key/name -- Dockerfiles, .gitignore files, or GitLab CI/CD YAML files, selected via kind. Metadata only (no template body); call gitlab_get_template for one template's full content.")]
    public async Task<CallToolResult> ListTemplatesAsync(
        [Description("Which template catalog to list: \"dockerfile\", \"gitignore\", or \"ci_yml\".")]
        string kind,
        [Description("Maximum templates to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var normalizedKind = kind.ToLowerInvariant();
        if (normalizedKind is not ("dockerfile" or "gitignore" or "ci_yml"))
            throw new McpException("kind must be \"dockerfile\", \"gitignore\", or \"ci_yml\".");

        var options = new TemplateListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<TemplateCatalogEntry> collected = [];
        var truncated = false;

        if (normalizedKind == "dockerfile")
            await foreach (var template in templates.ListDockerfilesAsync(options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(TemplateMapper.ToCatalogEntry(template));
            }
        else if (normalizedKind == "gitignore")
            await foreach (var template in templates.ListGitignoresAsync(options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(TemplateMapper.ToCatalogEntry(template));
            }
        else
            await foreach (var template in templates.ListCiYmlsAsync(options, cancellationToken))
            {
                if (collected.Count == limit)
                {
                    truncated = true;
                    break;
                }

                collected.Add(TemplateMapper.ToCatalogEntry(template));
            }

        var source = normalizedKind switch
        {
            "dockerfile" => "templates/dockerfiles",
            "gitignore" => "templates/gitignores",
            _ => "templates/gitlab_ci_ymls"
        };

        return GitLabContent.Wrap(new TemplateCatalogListResult(collected, truncated), source);
    }

    [McpServerTool(Name = "gitlab_get_template", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one built-in scaffolding template's full body by name -- a Dockerfile, a .gitignore file, or a GitLab CI/CD YAML file, selected via kind. Pass the template's exact key as reported by gitlab_list_templates.")]
    public async Task<CallToolResult> GetTemplateAsync(
        [Description("Which template catalog to read from: \"dockerfile\", \"gitignore\", or \"ci_yml\".")]
        string kind,
        [Description(
            "The template's key/name, exactly as reported by gitlab_list_templates (e.g. \"Node\", \"Ruby.gitignore\", \"Auto-DevOps\").")]
        string name,
        CancellationToken cancellationToken)
    {
        var normalizedKind = kind.ToLowerInvariant();

        GitLabTemplate template;
        string source;

        if (normalizedKind == "dockerfile")
        {
            template = await templates.GetDockerfileAsync(name, cancellationToken);
            source = "templates/dockerfiles/:name";
        }
        else if (normalizedKind == "gitignore")
        {
            template = await templates.GetGitignoreAsync(name, cancellationToken);
            source = "templates/gitignores/:name";
        }
        else if (normalizedKind == "ci_yml")
        {
            template = await templates.GetCiYmlAsync(name, cancellationToken);
            source = "templates/gitlab_ci_ymls/:name";
        }
        else
        {
            throw new McpException("kind must be \"dockerfile\", \"gitignore\", or \"ci_yml\".");
        }

        return GitLabContent.Wrap(TemplateMapper.ToDetail(template), source);
    }

    [McpServerTool(Name = "gitlab_get_license_template", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one open-source license's full text by key (e.g. \"mit\", \"apache-2.0\"), optionally with the [project] and [fullname] placeholders in the text filled in for direct use in a new repository.")]
    public async Task<CallToolResult> GetLicenseTemplateAsync(
        [Description(
            "The license key or name, exactly as reported by gitlab_list_license_templates (e.g. \"mit\", \"apache-2.0\", \"gpl-3.0\").")]
        string key,
        [Description(
            "The project's name, to substitute for the [project] placeholder in the license text. Templates with no such placeholder ignore it. Omit to leave any [project] placeholder unfilled.")]
        string? projectName = null,
        [Description(
            "The copyright holder's full name, to substitute for the [fullname] placeholder in the license text. Omit to leave any [fullname] placeholder unfilled.")]
        string? fullName = null,
        CancellationToken cancellationToken = default)
    {
        var template = await templates.GetLicenseAsync(key, projectName, fullName, cancellationToken);
        return GitLabContent.Wrap(TemplateMapper.ToLicenseDetail(template), "templates/licenses/:key");
    }

    [McpServerTool(Name = "gitlab_get_group_activity_summary", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets a group's recent-activity snapshot in one call: issues created, merge requests created, and new members added. GitLab does not document the exact lookback window these counts cover.")]
    public async Task<GroupActivitySummaryResult> GetGroupActivitySummaryAsync(
        [Description(
            "The group's full path, e.g. \"my-group\" or \"my-group/subgroup\" -- this endpoint takes the path only, not a numeric id.")]
        string groupPath,
        CancellationToken cancellationToken)
    {
        var issuesCount = await analytics.GetGroupActivityIssuesCountAsync(groupPath, cancellationToken);
        var mergeRequestsCount = await analytics.GetGroupActivityMergeRequestsCountAsync(groupPath, cancellationToken);
        var newMembersCount = await analytics.GetGroupActivityNewMembersCountAsync(groupPath, cancellationToken);

        return new GroupActivitySummaryResult(
            groupPath,
            issuesCount.IssuesCount,
            mergeRequestsCount.MergeRequestsCount,
            newMembersCount.NewMembersCount);
    }

    [McpServerTool(Name = "gitlab_list_deployment_frequency", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Streams a project's deployment-frequency series for one environment over a date range, one point per period.")]
    public async Task<CallToolResult> ListDeploymentFrequencyAsync(
        [Description("Project: numeric project id (\"42\") or URL-encoded path (\"group/subgroup/project\").")]
        string project,
        [Description("The environment's name exactly as it appears in GitLab, e.g. \"production\".")]
        string environment,
        [Description("Start of the date range, ISO 8601 (\"YYYY-MM-DD\").")]
        string from,
        [Description("End of the date range, ISO 8601 (\"YYYY-MM-DD\"). Omit for GitLab's default (today).")]
        string? to = null,
        [Description("Bucket size for the series: \"daily\", \"monthly\", or \"all\". Omit for GitLab's default.")]
        string? interval = null,
        [Description("Maximum points to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        ValidateIsoDate(from, nameof(from));
        if (to is not null) ValidateIsoDate(to, nameof(to));

        if (interval is not (null or "daily" or "monthly" or "all"))
            throw new McpException("interval must be \"daily\", \"monthly\", \"all\", or omitted.");

        var options = new DeploymentFrequencyListOptions { To = to, Interval = interval };

        List<DeploymentFrequencyPointSummary> collected = [];
        var truncated = false;

        await foreach (var point in analytics.ListDeploymentFrequencyAsync((ProjectId)project, environment, from,
                           options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AnalyticsMapper.ToSummary(point));
        }

        return GitLabContent.Wrap(new DeploymentFrequencyListResult(collected, truncated),
            "projects/:id/analytics/deployment_frequency");
    }

    [McpServerTool(Name = "gitlab_get_dora_metrics", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets a DORA metric time series for a group or a project: deployment frequency, lead time for changes, time to restore service, or change failure rate. DORA metrics require GitLab Premium or Ultimate -- on a Free instance this fails rather than returning zeros.")]
    public async Task<CallToolResult> GetDoraMetricsAsync(
        [Description(
            "Which metric to fetch: \"deployment_frequency\", \"lead_time_for_changes\", \"time_to_restore_service\", or \"change_failure_rate\".")]
        string metric,
        [Description("Whether id identifies a \"group\" or a \"project\".")]
        string scope,
        [Description("The group's or project's numeric id or URL-encoded path, matching scope.")]
        string id,
        [Description("Start of the date range, ISO 8601 (\"YYYY-MM-DD\"). Omit for GitLab's default (3 months ago).")]
        string? startDate = null,
        [Description("End of the date range, ISO 8601 (\"YYYY-MM-DD\"). Omit for GitLab's default (today).")]
        string? endDate = null,
        [Description("Bucket size for the series: \"daily\", \"monthly\", or \"all\". Omit for GitLab's default.")]
        string? interval = null,
        [Description(
            "Comma-separated environment tier names to restrict the metric to, e.g. \"production\". Omit for GitLab's default (production only).")]
        string? environmentTiers = null,
        [Description("Maximum points to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (metric is not ("deployment_frequency" or "lead_time_for_changes" or "time_to_restore_service"
            or "change_failure_rate"))
            throw new McpException(
                "metric must be \"deployment_frequency\", \"lead_time_for_changes\", \"time_to_restore_service\", or \"change_failure_rate\".");

        if (startDate is not null) ValidateIsoDate(startDate, nameof(startDate));

        if (endDate is not null) ValidateIsoDate(endDate, nameof(endDate));

        if (interval is not (null or "daily" or "monthly" or "all"))
            throw new McpException("interval must be \"daily\", \"monthly\", \"all\", or omitted.");

        var options = new DoraMetricsOptions
        {
            StartDate = startDate,
            EndDate = endDate,
            Interval = interval,
            EnvironmentTiers = ParseStringList(environmentTiers)
        };

        JsonElement root;
        string source;
        string scopeLabel;

        if (string.Equals(scope, "group", StringComparison.OrdinalIgnoreCase))
        {
            root = await analytics.GetGroupDoraMetricsAsync((GroupId)id, metric, options, cancellationToken);
            source = "groups/:id/dora/metrics";
            scopeLabel = "group";
        }
        else if (string.Equals(scope, "project", StringComparison.OrdinalIgnoreCase))
        {
            root = await analytics.GetProjectDoraMetricsAsync((ProjectId)id, metric, options, cancellationToken);
            source = "projects/:id/dora/metrics";
            scopeLabel = "project";
        }
        else
        {
            throw new McpException("scope must be \"group\" or \"project\".");
        }

        var (points, truncated) = AnalyticsMapper.ParseDoraMetrics(root, limit);
        return GitLabContent.Wrap(new DoraMetricsResult(scopeLabel, metric, points, truncated), source);
    }

    [McpServerTool(Name = "gitlab_list_code_search_shards", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every Zoekt exact-code-search node configured on this self-managed instance, for capacity and health checks. Requires administrator access; GitLab returns 403 for any other token.")]
    public async Task<CallToolResult> ListCodeSearchShardsAsync(
        [Description("Maximum nodes to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var all = await codeSearch.ListShardsAsync(cancellationToken);
        var truncated = all.Count > limit;
        var collected = all.Take(limit).Select(CodeSearchMapper.ToSummary).ToList();

        return GitLabContent.Wrap(new ZoektNodeListResult(collected, truncated), "admin/zoekt/nodes");
    }

    [McpServerTool(Name = "gitlab_list_active_context_connections", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every ActiveContext (Duo agentic indexing) connection configured on this instance and which one is active. Requires administrator access; GitLab returns 403 for any other token.")]
    public async Task<CallToolResult> ListActiveContextConnectionsAsync(
        [Description("Maximum connections to return (1-100). Default 50.")]
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var all = await activeContext.ListConnectionsAsync(cancellationToken);
        var truncated = all.Count > limit;
        var collected = all.Take(limit).Select(ActiveContextMapper.ToConnectionSummary).ToList();

        return GitLabContent.Wrap(new ActiveContextConnectionListResult(collected, truncated),
            "admin/active_context/connections");
    }

    // ---------------------------------------------------------------------------------------------
    // Writes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_active_context_collection", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Adjusts an ActiveContext collection's queue-sharding options (shard count/limit), for scaling its indexing throughput. Every field is optional; an omitted field leaves that setting unchanged. Instance-administrator access required.")]
    public async Task<CallToolResult> UpdateActiveContextCollectionAsync(
        [Description("The collection's name (e.g. \"gitlab_active_context_code\") or numeric id.")]
        string id,
        [Description("New number of queue shards for this collection. Omit to leave unchanged.")]
        int? queueShardCount = null,
        [Description("New per-shard queue limit for this collection. Omit to leave unchanged.")]
        int? queueShardLimit = null,
        [Description(
            "Reassign this collection to a different ActiveContext connection, by its numeric id. Omit to leave unchanged.")]
        long? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateActiveContextCollectionRequest
        {
            QueueShardCount = queueShardCount,
            QueueShardLimit = queueShardLimit,
            ConnectionId = connectionId
        };

        var detail = await activeContext.UpdateCollectionAsync(id, request, cancellationToken);
        return GitLabContent.Wrap(ActiveContextMapper.ToResult(detail),
            "admin/active_context/collections/:id (update)");
    }

    [McpServerTool(Name = "gitlab_create_wiki_page", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new wiki page (title + markdown content) in a project's or group's wiki. Give exactly one of project or group. GitLab derives the page's slug from the title. Group wikis require GitLab Premium or Ultimate.")]
    public async Task<CallToolResult> CreateWikiPageAsync(
        [Description("The new page's title.")] string title,
        [Description("The page's content, in the format given by format (default markdown).")]
        string content,
        [Description("Project: numeric project id or URL-encoded path. Omit if group is given instead.")]
        string? project = null,
        [Description("Group: numeric group id or URL-encoded path. Omit if project is given instead.")]
        string? group = null,
        [Description(
            "Content format: \"markdown\", \"rdoc\", \"asciidoc\", or \"org\". Omit for GitLab's default (markdown).")]
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        var request = new CreateWikiPageRequest
        {
            Title = title,
            Content = content,
            Format = format
        };

        if (hasProject)
        {
            var page = await wikis.CreateForProjectAsync((ProjectId)project!, request, cancellationToken);
            return GitLabContent.Wrap(WikiMapper.ToSummary(page), "projects/:id/wikis (create)");
        }

        var groupPage = await wikis.CreateForGroupAsync((GroupId)group!, request, cancellationToken);
        return GitLabContent.Wrap(WikiMapper.ToSummary(groupPage), "groups/:id/wikis (create)");
    }

    [McpServerTool(Name = "gitlab_add_code_search_indexed_namespace", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Adds a namespace to a Zoekt node's exact-code-search index. Instance-administrator access required.")]
    public async Task<ZoektIndexedNamespaceSummary> AddCodeSearchIndexedNamespaceAsync(
        [Description("The Zoekt node's numeric id to add the namespace to.")]
        long nodeId,
        [Description("The numeric id of the namespace (group or user) to index.")]
        long namespaceId,
        [Description(
            "If true, enables code search (not just indexing) for this namespace on this node. Omit for GitLab's default.")]
        bool? enableSearch = null,
        CancellationToken cancellationToken = default)
    {
        var request = enableSearch is null ? null : new AddZoektIndexedNamespaceRequest { Search = enableSearch };
        var ns = await codeSearch.AddIndexedNamespaceAsync(nodeId, namespaceId, request, cancellationToken);
        return CodeSearchMapper.ToSummary(ns);
    }

    [McpServerTool(Name = "gitlab_create_snippet", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a personal snippet, or a project snippet when project is given (project snippets require visibility to be set). Supply either fileName+content for a single-file snippet, or files for a multi-file one — not both.")]
    public async Task<CallToolResult> CreateSnippetAsync(
        [Description("The snippet's title.")] string title,
        [Description("Optional description.")] string? description = null,
        [Description(
            "Visibility: \"private\", \"internal\", or \"public\". Required when project is given; for a personal snippet, omitting it defaults to \"internal\".")]
        string? visibility = null,
        [Description(
            "For a single-file snippet: the file's name, e.g. \"script.py\". Use together with content; omit when files is given instead.")]
        string? fileName = null,
        [Description(
            "For a single-file snippet: the file's content. Use together with fileName; omit when files is given instead.")]
        string? content = null,
        [Description(
            "For a multi-file snippet: the list of files (each with a filePath and content). Omit when fileName/content is given instead.")]
        IReadOnlyList<SnippetFileInput>? files = null,
        [Description(
            "Project: numeric project id or URL-encoded path, to create a project snippet instead of a personal one. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (fileName is not null != content is not null)
            throw new McpException(
                "fileName and content must be given together for a single-file snippet — you supplied only one of the two.");

        var hasSingleFile = fileName is not null && content is not null;
        var hasMultiFile = files is { Count: > 0 };
        if (hasSingleFile == hasMultiFile)
            throw new McpException(
                "Give either fileName+content for a single file, or files for multiple files — not both, not neither.");

        GitLabVisibility? gitLabVisibility = visibility?.ToLowerInvariant() switch
        {
            "private" => GitLabVisibility.Private,
            "internal" => GitLabVisibility.Internal,
            "public" => GitLabVisibility.Public,
            null or "" => null,
            _ => throw new McpException("visibility must be \"private\", \"internal\", \"public\", or omitted.")
        };

        var hasProject = !string.IsNullOrEmpty(project);
        if (hasProject && gitLabVisibility is null)
            throw new McpException("visibility is required when creating a project snippet.");

        var request = new CreateSnippetRequest
        {
            Title = title,
            Description = description,
            Visibility = gitLabVisibility,
            FileName = hasSingleFile ? fileName : null,
            Content = hasSingleFile ? content : null,
            Files = hasMultiFile
                ? files!.Select(static f => new CreateSnippetFileRequest { FilePath = f.FilePath, Content = f.Content })
                    .ToList()
                : null
        };

        var snippet = hasProject
            ? await snippets.CreateForProjectAsync((ProjectId)project!, request, cancellationToken)
            : await snippets.CreateAsync(request, cancellationToken);

        return GitLabContent.Wrap(SnippetMapper.ToSummary(snippet),
            hasProject ? "projects/:id/snippets (create)" : "snippets (create)");
    }

    [McpServerTool(Name = "gitlab_disable_knowledge_graph_namespace", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Disables GitLab Duo Knowledge Graph for a namespace, removing its graph indexing. This cannot be undone from this tool — the namespace must be re-enabled separately. Instance-administrator access required.")]
    public async Task<KnowledgeGraphDisableResult> DisableKnowledgeGraphNamespaceAsync(
        [Description("The namespace's numeric id or its URL-encoded full path.")]
        string namespaceId,
        CancellationToken cancellationToken)
    {
        await knowledgeGraph.DisableNamespaceAsync(namespaceId, cancellationToken);
        return new KnowledgeGraphDisableResult(namespaceId, true);
    }

    [McpServerTool(Name = "gitlab_update_wiki_page", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates an existing wiki page's title, content or format by slug, in a project's or group's wiki. Give exactly one of project or group, and at least one of newTitle, content or format. Group wikis require GitLab Premium or Ultimate.")]
    public async Task<CallToolResult> UpdateWikiPageAsync(
        [Description("The page's slug to update, exactly as reported by gitlab_list_wiki_pages.")]
        string slug,
        [Description("Project: numeric project id or URL-encoded path. Omit if group is given instead.")]
        string? project = null,
        [Description("Group: numeric group id or URL-encoded path. Omit if project is given instead.")]
        string? group = null,
        [Description("New title for the page. Omit to leave unchanged.")]
        string? newTitle = null,
        [Description("New content for the page. Omit to leave unchanged.")]
        string? content = null,
        [Description("New content format: \"markdown\", \"rdoc\", \"asciidoc\", or \"org\". Omit to leave unchanged.")]
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        if (newTitle is null && content is null && format is null)
            throw new McpException("Give at least one of newTitle, content, or format to update.");

        var request = new UpdateWikiPageRequest
        {
            Title = newTitle,
            Content = content,
            Format = format
        };

        if (hasProject)
        {
            var page = await wikis.UpdateForProjectAsync((ProjectId)project!, slug, request, cancellationToken);
            return GitLabContent.Wrap(WikiMapper.ToSummary(page), "projects/:id/wikis/:slug (update)");
        }

        var groupPage = await wikis.UpdateForGroupAsync((GroupId)group!, slug, request, cancellationToken);
        return GitLabContent.Wrap(WikiMapper.ToSummary(groupPage), "groups/:id/wikis/:slug (update)");
    }

    [McpServerTool(Name = "gitlab_delete_wiki_page", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes one wiki page by slug from a project's or group's wiki. Give exactly one of project or group. This cannot be undone.")]
    public async Task<WikiPageDeleteResult> DeleteWikiPageAsync(
        [Description("The page's slug to delete, exactly as reported by gitlab_list_wiki_pages.")]
        string slug,
        [Description("Project: numeric project id or URL-encoded path. Omit if group is given instead.")]
        string? project = null,
        [Description("Group: numeric group id or URL-encoded path. Omit if project is given instead.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        if (hasProject)
            await wikis.DeleteForProjectAsync((ProjectId)project!, slug, cancellationToken);
        else
            await wikis.DeleteForGroupAsync((GroupId)group!, slug, cancellationToken);

        return new WikiPageDeleteResult(slug, true);
    }

    [McpServerTool(Name = "gitlab_upload_wiki_attachment", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Uploads a file into a project's or group's wiki uploads directory, without creating or editing a page. The result's markdown is ready to paste into a page body to embed the file. Give exactly one of project or group. Group wikis require GitLab Premium or Ultimate.")]
    public async Task<CallToolResult> UploadWikiAttachmentAsync(
        [Description("Base64-encoded content of the file to upload.")]
        string fileContentBase64,
        [Description("Filename for the upload, e.g. \"diagram.png\".")]
        string fileName,
        [Description("Project: numeric project id or URL-encoded path. Omit if group is given instead.")]
        string? project = null,
        [Description("Group: numeric group id or URL-encoded path. Omit if project is given instead.")]
        string? group = null,
        [Description("The wiki branch to commit the attachment to. Omit for the wiki's default branch.")]
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        var hasProject = !string.IsNullOrEmpty(project);
        var hasGroup = !string.IsNullOrEmpty(group);
        if (hasProject == hasGroup) throw new McpException("Give exactly one of project or group.");

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(fileContentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("fileContentBase64 is not valid base64.");
        }

        await using var fileStream = new MemoryStream(fileBytes);
        var upload = new GitLabFileUpload { Content = fileStream, FileName = fileName };

        if (hasProject)
        {
            var attachment =
                await wikis.UploadAttachmentForProjectAsync((ProjectId)project!, upload, branch, cancellationToken);
            return GitLabContent.Wrap(WikiMapper.ToAttachmentSummary(attachment), "projects/:id/wikis/attachments");
        }

        var groupAttachment =
            await wikis.UploadAttachmentForGroupAsync((GroupId)group!, upload, branch, cancellationToken);
        return GitLabContent.Wrap(WikiMapper.ToAttachmentSummary(groupAttachment), "groups/:id/wikis/attachments");
    }

    [McpServerTool(Name = "gitlab_update_snippet", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a personal snippet, or (with project given) a project snippet. Every field is optional except snippetId; an omitted field leaves that setting unchanged. Supply either fileName+content to update a single-file snippet's file, or files to apply a set of changes to a multi-file snippet — not both.")]
    public async Task<CallToolResult> UpdateSnippetAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description("New title. Omit to leave unchanged.")]
        string? title = null,
        [Description("New description. Omit to leave unchanged.")]
        string? description = null,
        [Description("New visibility: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? visibility = null,
        [Description(
            "For a single-file snippet: the file's new name. Use together with content; omit when files is given instead.")]
        string? fileName = null,
        [Description(
            "For a single-file snippet: the file's new content. Use together with fileName; omit when files is given instead.")]
        string? content = null,
        [Description(
            "For a multi-file snippet: the file changes to apply (each an action of \"create\", \"update\", \"delete\" or \"move\", a filePath, and content or previousPath as that action requires). Omit when fileName/content is given instead.")]
        IReadOnlyList<UpdateSnippetFileInput>? files = null,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (fileName is not null != content is not null)
            throw new McpException(
                "fileName and content must be given together for a single-file update — you supplied only one of the two.");

        var hasSingleFile = fileName is not null && content is not null;
        var hasMultiFile = files is { Count: > 0 };
        if (hasSingleFile && hasMultiFile)
            throw new McpException(
                "Give either fileName+content for a single file, or files for multiple files — not both.");

        GitLabVisibility? gitLabVisibility = visibility?.ToLowerInvariant() switch
        {
            "private" => GitLabVisibility.Private,
            "internal" => GitLabVisibility.Internal,
            "public" => GitLabVisibility.Public,
            null or "" => null,
            _ => throw new McpException("visibility must be \"private\", \"internal\", \"public\", or omitted.")
        };

        var request = new UpdateSnippetRequest
        {
            Title = title,
            Description = description,
            Visibility = gitLabVisibility,
            FileName = hasSingleFile ? fileName : null,
            Content = hasSingleFile ? content : null,
            Files = hasMultiFile ? files!.Select(ToUpdateFileRequest).ToList() : null
        };

        var hasProject = !string.IsNullOrEmpty(project);
        var snippet = hasProject
            ? await snippets.UpdateForProjectAsync((ProjectId)project!, snippetId, request, cancellationToken)
            : await snippets.UpdateAsync(snippetId, request, cancellationToken);

        return GitLabContent.Wrap(
            SnippetMapper.ToSummary(snippet),
            hasProject ? "projects/:id/snippets/:snippet_id (update)" : "snippets/:id (update)");
    }

    [McpServerTool(Name = "gitlab_delete_snippet", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes a personal snippet, or (with project given) a project snippet. This cannot be undone.")]
    public async Task<SnippetDeleteResult> DeleteSnippetAsync(
        [Description("The snippet's numeric id.")]
        long snippetId,
        [Description(
            "Project: numeric project id or URL-encoded path, when snippetId identifies a project snippet. Omit for a personal snippet.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(project))
            await snippets.DeleteAsync(snippetId, cancellationToken);
        else
            await snippets.DeleteForProjectAsync((ProjectId)project, snippetId, cancellationToken);

        return new SnippetDeleteResult(snippetId, true);
    }

    [McpServerTool(Name = "gitlab_remove_code_search_indexed_namespace", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Removes a namespace from a Zoekt node's exact-code-search index. The namespace's code stops being findable via exact code search on that node until it is added and re-indexed. Requires administrator access.")]
    public async Task<ZoektIndexedNamespaceRemoveResult> RemoveCodeSearchIndexedNamespaceAsync(
        [Description("The Zoekt node's numeric id to remove the namespace from.")]
        long nodeId,
        [Description("The numeric id of the namespace (group or user) to remove from the index.")]
        long namespaceId,
        CancellationToken cancellationToken)
    {
        await codeSearch.RemoveIndexedNamespaceAsync(nodeId, namespaceId, cancellationToken);
        return new ZoektIndexedNamespaceRemoveResult(nodeId, namespaceId, true);
    }

    [McpServerTool(Name = "gitlab_update_code_search_namespace_replicas", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Overrides how many index replicas a namespace gets in Zoekt exact-code-search, for scaling search capacity. Requires administrator access.")]
    public async Task<ZoektIndexedNamespaceSummary> UpdateCodeSearchNamespaceReplicasAsync(
        [Description("The enabled namespace's numeric id or its URL-encoded full path.")]
        string id,
        [Description(
            "The new replica-count override. Omit (or pass null) to clear the override and fall back to the node's default replica count.")]
        int? numberOfReplicasOverride = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateZoektNamespaceReplicasRequest { NumberOfReplicasOverride = numberOfReplicasOverride };
        var ns = await codeSearch.UpdateNamespaceReplicasAsync(id, request, cancellationToken);
        return CodeSearchMapper.ToSummary(ns);
    }

    [McpServerTool(Name = "gitlab_trigger_code_search_indexing", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Triggers a fresh Zoekt exact-code-search reindex of one project and returns the background job id GitLab enqueued to track it. Requires administrator access.")]
    public async Task<CallToolResult> TriggerCodeSearchIndexingAsync(
        [Description("The project's numeric id to reindex.")]
        long projectId,
        CancellationToken cancellationToken)
    {
        var result = await codeSearch.IndexProjectAsync(projectId, cancellationToken);
        return GitLabContent.Wrap(CodeSearchMapper.ToIndexResult(result), "admin/zoekt/projects/:project_id/index");
    }

    [McpServerTool(Name = "gitlab_enable_knowledge_graph_namespace", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Enables GitLab Duo Knowledge Graph for a namespace. Requires administrator access.")]
    public async Task<KnowledgeGraphNamespaceSummary> EnableKnowledgeGraphNamespaceAsync(
        [Description("The namespace's numeric id or its URL-encoded full path.")]
        string namespaceId,
        CancellationToken cancellationToken)
    {
        var ns = await knowledgeGraph.EnableNamespaceAsync(namespaceId, cancellationToken);
        return KnowledgeGraphMapper.ToSummary(ns);
    }

    [McpServerTool(Name = "gitlab_activate_active_context_connection", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Activates an ActiveContext connection. This deactivates and drops the indexed data of whichever connection was previously active -- treat as irreversible. Requires administrator access.")]
    public async Task<CallToolResult> ActivateActiveContextConnectionAsync(
        [Description(
            "The numeric id of the connection to activate, exactly as reported by gitlab_list_active_context_connections.")]
        long connectionId,
        CancellationToken cancellationToken)
    {
        var request = new ActivateActiveContextConnectionRequest { ConnectionId = connectionId };
        var connection = await activeContext.ActivateConnectionAsync(request, cancellationToken);
        return GitLabContent.Wrap(ActiveContextMapper.ToConnectionSummary(connection),
            "admin/active_context/connections/activate");
    }

    [McpServerTool(Name = "gitlab_deactivate_active_context_connection", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Deactivates an ActiveContext connection -- the currently active one if connectionId is omitted. Asynchronously drops its indexed data and deletes its record; this cannot be undone. Requires administrator access.")]
    public async Task<CallToolResult> DeactivateActiveContextConnectionAsync(
        [Description(
            "The numeric id of the connection to deactivate. Omit to deactivate whichever connection is currently active.")]
        long? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DeactivateActiveContextConnectionRequest { ConnectionId = connectionId };
        var connection = await activeContext.DeactivateConnectionAsync(request, cancellationToken);
        return GitLabContent.Wrap(ActiveContextMapper.ToConnectionSummary(connection),
            "admin/active_context/connections/deactivate");
    }

    [McpServerTool(Name = "gitlab_update_active_context_namespace_state", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Transitions a namespace's ActiveContext code-indexing state between \"pending\" and \"ready\", e.g. to force a re-sync. Requires administrator access.")]
    public async Task<CallToolResult> UpdateActiveContextNamespaceStateAsync(
        [Description("The namespace's numeric id or its URL-encoded full path.")]
        string namespaceId,
        [Description("The state to transition to: \"pending\" or \"ready\".")]
        string state,
        [Description(
            "Reassign this namespace to a different ActiveContext connection, by its numeric id. Omit to leave the current connection unchanged.")]
        long? connectionId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedState = state.ToLowerInvariant() switch
        {
            "pending" => GitLabActiveContextNamespaceState.Pending,
            "ready" => GitLabActiveContextNamespaceState.Ready,
            _ => throw new McpException("state must be \"pending\" or \"ready\".")
        };

        var request = new UpdateActiveContextEnabledNamespaceStateRequest
        {
            NamespaceId = BuildNamespaceIdElement(namespaceId),
            State = normalizedState,
            ConnectionId = connectionId
        };

        var ns = await activeContext.UpdateEnabledNamespaceStateAsync(request, cancellationToken);
        return GitLabContent.Wrap(ActiveContextMapper.ToNamespaceStateResult(ns),
            "admin/active_context/code/enabled_namespaces");
    }

    [McpServerTool(Name = "gitlab_clear_active_context_dead_queue", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Permanently discards every item currently stuck in the ActiveContext (Duo agentic indexing) dead-letter queue, without attempting to reprocess them -- use gitlab_replay_active_context_dead_queue instead if the items should be retried rather than dropped. This cannot be undone. Requires administrator access; GitLab returns 403 for any other token.")]
    public async Task<ActiveContextDeadQueueClearResult> ClearActiveContextDeadQueueAsync(
        CancellationToken cancellationToken)
    {
        await activeContext.ClearDeadQueueAsync(cancellationToken);
        return new ActiveContextDeadQueueClearResult(true);
    }

    [McpServerTool(Name = "gitlab_replay_active_context_dead_queue", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Enqueues a background task that re-enqueues every item in the ActiveContext (Duo agentic indexing) dead-letter queue into another named queue for reprocessing, as a recovery action after a transient indexing failure. This only starts the replay; it does not wait for it to finish. Requires administrator access; GitLab returns 403 for any other token.")]
    public async Task<ActiveContextDeadQueueReplayResult> ReplayActiveContextDeadQueueAsync(
        [Description(
            "The target queue name to re-enqueue the dead-lettered items into, e.g. \"retry_queue\", \"second_retry_queue\", \"code\", or \"code_backfill\".")]
        string queue,
        CancellationToken cancellationToken)
    {
        var request = new ReplayActiveContextDeadQueueRequest { Queue = queue };
        await activeContext.ReplayDeadQueueAsync(request, cancellationToken);
        return new ActiveContextDeadQueueReplayResult(queue, true);
    }

    // ---------------------------------------------------------------------------------------------

    private static void ValidateIsoDate(string value, string paramName)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new McpException($"{paramName} must be an ISO 8601 date (YYYY-MM-DD).");
    }

    /// <summary>
    ///     Builds the <see cref="JsonElement" /> <c>UpdateActiveContextEnabledNamespaceStateRequest.NamespaceId</c>
    ///     requires -- the GitLab spec types this field as either a JSON string or a JSON number, so the
    ///     library carries it untyped rather than forcing one C# shape (see the property's own XML doc).
    /// </summary>
    private static JsonElement BuildNamespaceIdElement(string namespaceId)
    {
        if (long.TryParse(namespaceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId))
        {
            using var numericDocument = JsonDocument.Parse(numericId.ToString(CultureInfo.InvariantCulture));
            return numericDocument.RootElement.Clone();
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(namespaceId);
        }

        using var stringDocument = JsonDocument.Parse(buffer.ToArray());
        return stringDocument.RootElement.Clone();
    }

    private static IReadOnlyList<string>? ParseStringList(string? csv)
    {
        return string.IsNullOrWhiteSpace(csv)
            ? null
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static UpdateSnippetFileRequest ToUpdateFileRequest(UpdateSnippetFileInput input)
    {
        return new UpdateSnippetFileRequest
        {
            Action = input.Action.ToLowerInvariant() switch
            {
                "create" => GitLabSnippetFileAction.Create,
                "update" => GitLabSnippetFileAction.Update,
                "delete" => GitLabSnippetFileAction.Delete,
                "move" => GitLabSnippetFileAction.Move,
                _ => throw new McpException(
                    $"Invalid file action \"{input.Action}\" in files — must be \"create\", \"update\", \"delete\", or \"move\".")
            },
            FilePath = input.FilePath,
            PreviousPath = input.PreviousPath,
            Content = input.Content
        };
    }
}