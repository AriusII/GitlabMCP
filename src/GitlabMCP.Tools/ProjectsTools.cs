using System.ComponentModel;
using GitLab.Client.Abstractions;
using GitLab.Client.Domain;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Projects;
using GitlabMCP.Mapping.Projects;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Projects/groups/namespaces tools. Constructor-injects only the narrow <c>I&lt;Resource&gt;Client</c>
///     interfaces this class needs, per CLAUDE.md's tool-authoring conventions. Every tool that can return
///     any GitLab-authored string (a name, path, description, filename, error message, ...) wraps via
///     <see cref="GitLabContent" />; the two delete tools return a bare scalar-only record instead, since
///     their result carries nothing but a caller-supplied id echo and a server-computed bool.
/// </summary>
[McpServerToolType]
public sealed class ProjectsTools(
    IGroupsClient groups,
    IProjectsClient projects,
    INamespacesClient namespaces,
    IProjectUploadsClient projectUploads,
    IProjectAliasesClient projectAliases,
    ITopicsClient topics,
    IBadgesClient badges,
    ICustomAttributesClient customAttributes,
    IAvatarsClient avatars,
    IStorageMovesClient storageMoves,
    IOrganizationsClient organizations)
{
    private const int MaxLimit = 100;

    /// <summary>
    ///     4 MB, matching the attestation-download precedent in InfraTools.cs: a markdown attachment is typically an
    ///     image/PDF and refusing an oversized one beats returning a truncated, unusable partial file.
    /// </summary>
    private const int MaxProjectUploadDownloadBytes = 4 * 1024 * 1024;

    /// <summary>
    ///     2 MB: avatars are small square images, so this is generous headroom over the real case rather than a tight
    ///     bound.
    /// </summary>
    private const int MaxAvatarDownloadBytes = 2 * 1024 * 1024;

    // ---------------------------------------------------------------- gitlab_list_group_issues

    [McpServerTool(Name = "gitlab_list_group_issues", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists issues across every project in a GitLab group and its subgroups. Returns a compact summary per issue (not the full description); use this for a cross-project planning view.")]
    public async Task<CallToolResult> ListGroupIssuesAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description(
            "Filter by state: \"opened\", \"closed\", \"all\", or omit to return issues of every state (GitLab's group-issues API applies no state filter when this is omitted).")]
        string? state = null,
        [Description("Optional free-text search over issue titles and descriptions.")]
        string? search = null,
        [Description("Maximum issues to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new GroupIssueListOptions
        {
            State = state switch
            {
                "opened" or "closed" or "all" => state,
                null or "" => null,
                _ => throw new McpException("state must be \"opened\", \"closed\", \"all\", or omitted.")
            },
            Search = string.IsNullOrEmpty(search) ? null : search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<GroupIssueSummary> collected = [];
        var truncated = false;

        await foreach (var issue in groups.ListIssuesAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToGroupIssueSummary(issue));
        }

        return GitLabContent.Wrap(new GroupIssueListResult(collected, truncated), "groups/:id/issues");
    }

    // ---------------------------------------------------------------- gitlab_list_project_forks

    [McpServerTool(Name = "gitlab_list_project_forks", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's forks: which users or groups have copied it.")]
    public async Task<CallToolResult> ListProjectForksAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum forks to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ProjectListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<ProjectSummary> collected = [];
        var truncated = false;

        await foreach (var fork in projects.ListForksAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(fork));
        }

        return GitLabContent.Wrap(new ProjectListResult(collected, truncated), "projects/:id/forks");
    }

    // ---------------------------------------------------------------- gitlab_list_namespace_storage_limit_exclusions

    [McpServerTool(Name = "gitlab_list_namespace_storage_limit_exclusions", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every namespace, instance-wide, that has been excluded from storage-limit enforcement. Administrator token required.")]
    public async Task<CallToolResult> ListNamespaceStorageLimitExclusionsAsync(
        [Description("Maximum exclusions to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new NamespaceStorageLimitExclusionListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<NamespaceStorageLimitExclusionSummary> collected = [];
        var truncated = false;

        await foreach (var exclusion in namespaces.ListStorageLimitExclusionsAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(exclusion));
        }

        return GitLabContent.Wrap(new NamespaceStorageLimitExclusionListResult(collected, truncated),
            "namespaces/storage/limit_exclusions");
    }

    // ---------------------------------------------------------------- gitlab_list_project_uploads

    [McpServerTool(Name = "gitlab_list_project_uploads", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists markdown-attachment uploads on a project (files attached to issues/MRs/comments via drag-and-drop). Requires Maintainer or Owner role on the project.")]
    public async Task<CallToolResult> ListProjectUploadsAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Maximum uploads to return, 1-100. Default 20. The result reports whether more exist. GitLab does not paginate this endpoint itself; this bound is enforced client-side.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ProjectUploadSummary> collected = [];
        var truncated = false;

        await foreach (var upload in projectUploads.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(upload));
        }

        return GitLabContent.Wrap(new ProjectUploadListResult(collected, truncated), "projects/:id/uploads");
    }

    // ---------------------------------------------------------------- gitlab_list_project_aliases

    [McpServerTool(Name = "gitlab_list_project_aliases", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every Git-level project alias on the instance (an alternate clone path GitLab answers to for a project). Premium/Ultimate, administrators only.")]
    public async Task<CallToolResult> ListProjectAliasesAsync(
        [Description("Maximum aliases to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ProjectAliasListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<ProjectAliasSummary> collected = [];
        var truncated = false;

        await foreach (var alias in projectAliases.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(alias));
        }

        return GitLabContent.Wrap(new ProjectAliasListResult(collected, truncated), "project_aliases");
    }

    // ---------------------------------------------------------------- gitlab_list_topics

    [McpServerTool(Name = "gitlab_list_topics", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists instance-wide project topics (tags projects can carry), sorted by how many projects carry each one.")]
    public async Task<CallToolResult> ListTopicsAsync(
        [Description("Optional free-text search over topic names.")]
        string? search = null,
        [Description("Maximum topics to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new TopicListOptions
        {
            Search = string.IsNullOrEmpty(search) ? null : search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<TopicSummary> collected = [];
        var truncated = false;

        await foreach (var topic in topics.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(topic));
        }

        return GitLabContent.Wrap(new TopicListResult(collected, truncated), "topics");
    }

    // ---------------------------------------------------------------- gitlab_list_badges

    [McpServerTool(Name = "gitlab_list_badges", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's or a group's badges (the small status images shown on the project/group page), including badges a project inherits from its groups. Pass exactly one of project or group.")]
    public async Task<CallToolResult> ListBadgesAsync(
        [Description(
            "Project to list badges for: numeric id or \"group/subgroup/project\". Mutually exclusive with group.")]
        string? project = null,
        [Description("Group to list badges for: numeric id or \"group/subgroup\". Mutually exclusive with project.")]
        string? group = null,
        [Description("Returns only the badge with this exact name, or omit for all.")]
        string? name = null,
        [Description("Maximum badges to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (string.IsNullOrEmpty(project) == string.IsNullOrEmpty(group))
            throw new McpException("Pass exactly one of project or group.");

        List<BadgeSummary> collected = [];
        var truncated = false;

        var stream = !string.IsNullOrEmpty(project)
            ? badges.ListForProjectAsync(project, name, cancellationToken)
            : badges.ListForGroupAsync(group!, name, cancellationToken);

        await foreach (var badge in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(badge));
        }

        var source = !string.IsNullOrEmpty(project) ? "projects/:id/badges" : "groups/:id/badges";
        return GitLabContent.Wrap(new BadgeListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_list_custom_attributes

    [McpServerTool(Name = "gitlab_list_custom_attributes", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists custom key/value attributes attached to a user, group, or project. Administrator token required.")]
    public async Task<CallToolResult> ListCustomAttributesAsync(
        [Description("Which kind of entity id identifies: \"user\", \"group\", or \"project\".")]
        string entityType,
        [Description(
            "The entity's id: a numeric user id for \"user\"; a numeric id or full path for \"group\"/\"project\".")]
        string id,
        [Description(
            "Maximum attributes to return, 1-100. Default 20. The result reports whether more exist. GitLab does not paginate this endpoint itself; this bound is enforced client-side.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var stream = entityType switch
        {
            "user" => customAttributes.ListForUserAsync(ParseUserId(id), cancellationToken),
            "group" => customAttributes.ListForGroupAsync(id, cancellationToken),
            "project" => customAttributes.ListForProjectAsync(id, cancellationToken),
            _ => throw new McpException("entityType must be \"user\", \"group\", or \"project\".")
        };

        List<CustomAttributeSummary> collected = [];
        var truncated = false;

        await foreach (var attribute in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(attribute));
        }

        return GitLabContent.Wrap(new CustomAttributeListResult(collected, truncated),
            $"{entityType}s/:id/custom_attributes");
    }

    // ---------------------------------------------------------------- gitlab_get_avatar_url_for_email

    [McpServerTool(Name = "gitlab_get_avatar_url_for_email", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Resolves the avatar image URL GitLab would serve for a public email address. Always answers with a URL (falls back to the configured avatar service, e.g. Gravatar) — this is not proof that an account with that email exists.")]
    public async Task<CallToolResult> GetAvatarUrlForEmailAsync(
        [Description("The public email address to resolve an avatar for (not necessarily a sign-in address).")]
        string email,
        [Description("Requested image size in pixels (square), or omit for the service default.")]
        int? size = null,
        CancellationToken cancellationToken = default)
    {
        var avatar = await avatars.GetForEmailAsync(email, size, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToResult(avatar), "avatar");
    }

    // ---------------------------------------------------------------- gitlab_list_storage_moves

    [McpServerTool(Name = "gitlab_list_storage_moves", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists Gitaly repository-storage moves for a project (background relocations of its Git data between storage shards). Self-managed instances only.")]
    public async Task<CallToolResult> ListStorageMovesAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Maximum moves to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new StorageMoveListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<StorageMoveSummary> collected = [];
        var truncated = false;

        await foreach (var move in storageMoves.ListForProjectAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(move));
        }

        return GitLabContent.Wrap(new StorageMoveListResult(collected, truncated),
            "projects/:id/repository_storage_moves");
    }

    // ---------------------------------------------------------------- gitlab_list_group_projects

    [McpServerTool(Name = "gitlab_list_group_projects", ReadOnly = true, OpenWorld = false)]
    [Description("Lists projects owned by a group, or (with shared=true) projects that have been shared into it.")]
    public async Task<CallToolResult> ListGroupProjectsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("If true, lists projects shared into the group instead of projects it owns. Default false.")]
        bool shared = false,
        [Description("Optional free-text search over project names/paths.")]
        string? search = null,
        [Description("Maximum projects to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<ProjectSummary> collected = [];
        var truncated = false;
        var perPage = Math.Min(limit + 1, MaxLimit);
        var searchTerm = string.IsNullOrEmpty(search) ? null : search;

        var stream = shared
            ? groups.ListSharedProjectsAsync(group,
                new GroupSharedProjectListOptions { Search = searchTerm, PerPage = perPage }, cancellationToken)
            : groups.ListProjectsAsync(group, new GroupProjectListOptions { Search = searchTerm, PerPage = perPage },
                cancellationToken);

        await foreach (var project in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(project));
        }

        var source = shared ? "groups/:id/projects/shared" : "groups/:id/projects";
        return GitLabContent.Wrap(new ProjectListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_create_ci_config_merge_request

    [McpServerTool(Name = "gitlab_create_ci_config_merge_request", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description("Opens a merge request that adds a starter .gitlab-ci.yml to a project that has none.")]
    public async Task<CallToolResult> CreateCiConfigMergeRequestAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var mergeRequest = await projects.CreateCiConfigMergeRequestAsync(project, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToResult(mergeRequest), "projects/:id/create_ci_config");
    }

    // ---------------------------------------------------------------- gitlab_create_group

    [McpServerTool(Name = "gitlab_create_group", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a top-level group, or a subgroup when a parent group id is given.")]
    public async Task<CallToolResult> CreateGroupAsync(
        [Description("The group's display name.")]
        string name,
        [Description("The group's URL path segment (lowercase, no spaces).")]
        string path,
        [Description("Optional group description.")]
        string? description = null,
        [Description("Visibility: \"private\", \"internal\", or \"public\". Omit for GitLab's default (private).")]
        string? visibility = null,
        [Description("Numeric id of the parent group, to create this as a subgroup. Omit for a top-level group.")]
        long? parentGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateGroupRequest
        {
            Name = name,
            Path = path,
            Description = description,
            Visibility = ParseVisibility(visibility),
            ParentId = parentGroupId
        };

        var group = await groups.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(group), "groups (create)");
    }

    // ---------------------------------------------------------------- gitlab_delete_topic

    [McpServerTool(Name = "gitlab_delete_topic", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes a topic. Projects that carried it simply lose the label; nothing else about them changes. Administrators only. This cannot be undone.")]
    public async Task<TopicDeleteResult> DeleteTopicAsync(
        [Description("The topic's numeric id (from gitlab_list_topics).")]
        long topicId,
        CancellationToken cancellationToken = default)
    {
        await topics.DeleteAsync(topicId, cancellationToken);
        return new TopicDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_create_badge

    [McpServerTool(Name = "gitlab_create_badge", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a badge (a small status image with a link) to a project or a group. Group badges are inherited by every project under it. Pass exactly one of project or group.")]
    public async Task<CallToolResult> CreateBadgeAsync(
        [Description(
            "Where the badge links to. May contain GitLab placeholders: %{project_path}, %{default_branch}, %{commit_sha}.")]
        string linkUrl,
        [Description("The badge image source URL. May contain the same GitLab placeholders as linkUrl.")]
        string imageUrl,
        [Description("Optional display name for the badge.")]
        string? name = null,
        [Description(
            "Project to add the badge to: numeric id or \"group/subgroup/project\". Mutually exclusive with group.")]
        string? project = null,
        [Description("Group to add the badge to: numeric id or \"group/subgroup\". Mutually exclusive with project.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(project) == string.IsNullOrEmpty(group))
            throw new McpException("Pass exactly one of project or group.");

        var request = new CreateBadgeRequest { LinkUrl = linkUrl, ImageUrl = imageUrl, Name = name };

        var badge = !string.IsNullOrEmpty(project)
            ? await badges.CreateForProjectAsync(project, request, cancellationToken)
            : await badges.CreateForGroupAsync(group!, request, cancellationToken);

        var source = !string.IsNullOrEmpty(project) ? "projects/:id/badges (create)" : "groups/:id/badges (create)";
        return GitLabContent.Wrap(ProjectMapper.ToSummary(badge), source);
    }

    // ---------------------------------------------------------------- gitlab_delete_project_upload

    [McpServerTool(Name = "gitlab_delete_project_upload", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a project upload (a markdown attachment), either by its numeric id or by its secret+filename pair. Pass exactly one of uploadId, or the secret+filename pair. This cannot be undone.")]
    public async Task<ProjectUploadDeleteResult> DeleteProjectUploadAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "The upload's numeric id (from gitlab_list_project_uploads). Mutually exclusive with secret/filename.")]
        long? uploadId = null,
        [Description(
            "The upload's secret path component, as embedded in its markdown URL. Requires filename too; mutually exclusive with uploadId.")]
        string? secret = null,
        [Description("The upload's original filename, paired with secret. Mutually exclusive with uploadId.")]
        string? filename = null,
        CancellationToken cancellationToken = default)
    {
        var bySecret = !string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(filename);

        if (uploadId is null == !bySecret)
            throw new McpException("Pass exactly one of uploadId, or the secret+filename pair.");

        if (uploadId is not null)
            await projectUploads.DeleteAsync(project, uploadId.Value, cancellationToken);
        else
            await projectUploads.DeleteBySecretAsync(project, secret!, filename!, cancellationToken);

        return new ProjectUploadDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_create_project_alias

    [McpServerTool(Name = "gitlab_create_project_alias", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a Git-level alias pointing at an existing project, so an imported project keeps answering to its old clone path. Premium/Ultimate, administrators only.")]
    public async Task<CallToolResult> CreateProjectAliasAsync(
        [Description("The project the alias points to: its numeric id or its full path, e.g. \"gitlab-org/gitlab\".")]
        string project,
        [Description("The alias name to create. Must be unique across the instance.")]
        string aliasName,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateProjectAliasRequest { ProjectId = project, Name = aliasName };
        var alias = await projectAliases.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(alias), "project_aliases (create)");
    }

    // ---------------------------------------------------------------- gitlab_set_custom_attribute

    [McpServerTool(Name = "gitlab_set_custom_attribute", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Creates or overwrites a custom key/value attribute on a user, group, or project. This is an upsert: GitLab creates the key if it is new and overwrites it otherwise. Administrator token required.")]
    public async Task<CallToolResult> SetCustomAttributeAsync(
        [Description("Which kind of entity id identifies: \"user\", \"group\", or \"project\".")]
        string entityType,
        [Description(
            "The entity's id: a numeric user id for \"user\"; a numeric id or full path for \"group\"/\"project\".")]
        string id,
        [Description("The attribute's key.")] string key,
        [Description("The value to store. GitLab keeps it as an opaque string.")]
        string value,
        CancellationToken cancellationToken = default)
    {
        var request = new SetCustomAttributeRequest { Value = value };

        var attribute = entityType switch
        {
            "user" => await customAttributes.SetForUserAsync(ParseUserId(id), key, request, cancellationToken),
            "group" => await customAttributes.SetForGroupAsync(id, key, request, cancellationToken),
            "project" => await customAttributes.SetForProjectAsync(id, key, request, cancellationToken),
            _ => throw new McpException("entityType must be \"user\", \"group\", or \"project\".")
        };

        return GitLabContent.Wrap(ProjectMapper.ToSummary(attribute),
            $"{entityType}s/:id/custom_attributes/:key (set)");
    }

    // ---------------------------------------------------------------- gitlab_get_project

    [McpServerTool(Name = "gitlab_get_project", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one GitLab project's full details by numeric id or namespaced path.")]
    public async Task<CallToolResult> GetProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var result = await projects.GetAsync(project, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(result), "projects/:id");
    }

    // ---------------------------------------------------------------- gitlab_list_projects

    [McpServerTool(Name = "gitlab_list_projects", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches or browses GitLab projects visible to the configured token, filtered by visibility, search text, ownership, or membership.")]
    public async Task<CallToolResult> ListProjectsAsync(
        [Description("Optional free-text search over project names, paths and descriptions.")]
        string? search = null,
        [Description("Filter by visibility: \"private\", \"internal\", \"public\", or omit for all.")]
        string? visibility = null,
        [Description("If true, return only projects the caller owns. Default false (any visible project).")]
        bool owned = false,
        [Description("If true, return only projects the caller is a member of. Default false.")]
        bool membership = false,
        [Description("Maximum projects to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ProjectListOptions
        {
            Search = string.IsNullOrEmpty(search) ? null : search,
            Visibility = ParseVisibility(visibility),
            Owned = owned ? true : null,
            Membership = membership ? true : null,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ProjectSummary> collected = [];
        var truncated = false;

        await foreach (var project in projects.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(project));
        }

        return GitLabContent.Wrap(new ProjectListResult(collected, truncated), "projects");
    }

    // ---------------------------------------------------------------- gitlab_create_project

    [McpServerTool(Name = "gitlab_create_project", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a GitLab project in the caller's personal namespace, or in a specific namespace when namespaceId is given. Supplying ownerUserId creates it on another user's behalf instead (CreateForUserAsync) and requires administrator access.")]
    public async Task<CallToolResult> CreateProjectAsync(
        [Description("The new project's name.")]
        string name,
        [Description("The project's URL path segment (lowercase, no spaces). Omit to let GitLab derive it from name.")]
        string? path = null,
        [Description("Optional project description.")]
        string? description = null,
        [Description("Visibility: \"private\", \"internal\", or \"public\". Omit for the instance default.")]
        string? visibility = null,
        [Description(
            "Numeric id of the namespace (group) to create the project in. Omit to use the caller's personal namespace.")]
        long? namespaceId = null,
        [Description(
            "Numeric id of a user to create this project on behalf of, via CreateForUserAsync. Requires administrator access. Omit to create it for the caller.")]
        long? ownerUserId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateProjectRequest
        {
            Name = name,
            Path = path,
            Description = description,
            Visibility = ParseVisibility(visibility),
            NamespaceId = namespaceId
        };

        var created = ownerUserId is { } userId
            ? await projects.CreateForUserAsync(userId, request, cancellationToken)
            : await projects.CreateAsync(request, cancellationToken);

        return GitLabContent.Wrap(
            ProjectMapper.ToSummary(created),
            ownerUserId is null ? "projects (create)" : "projects/user/:user_id (create)");
    }

    // ---------------------------------------------------------------- gitlab_update_project

    [McpServerTool(Name = "gitlab_update_project", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a project's settings (name, description, visibility, default branch) and, when an avatar image is supplied, replaces its avatar in the same call.")]
    public async Task<CallToolResult> UpdateProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("New project name. Omit to leave unchanged.")]
        string? name = null,
        [Description("New project description. Omit to leave unchanged.")]
        string? description = null,
        [Description("New visibility: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? visibility = null,
        [Description("New default branch name. Omit to leave unchanged.")]
        string? defaultBranch = null,
        [Description(
            "Base64-encoded avatar image to set as the project's new avatar. Requires avatarFileName. Omit to leave the avatar unchanged.")]
        string? avatarBase64 = null,
        [Description(
            "Filename for the avatar image, e.g. \"logo.png\". Required together with avatarBase64; ignored otherwise.")]
        string? avatarFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(avatarBase64) != string.IsNullOrEmpty(avatarFileName))
            throw new McpException("avatarBase64 and avatarFileName must be supplied together.");

        var request = new UpdateProjectRequest
        {
            Name = name,
            Description = description,
            Visibility = ParseVisibility(visibility),
            DefaultBranch = defaultBranch
        };

        var updated = await projects.UpdateAsync(project, request, cancellationToken);

        if (!string.IsNullOrEmpty(avatarBase64))
        {
            byte[] avatarBytes;
            try
            {
                avatarBytes = Convert.FromBase64String(avatarBase64);
            }
            catch (FormatException)
            {
                throw new McpException("avatarBase64 is not valid base64.");
            }

            await using var avatarStream = new MemoryStream(avatarBytes);
            updated = await projects.SetAvatarAsync(
                project,
                new GitLabFileUpload { Content = avatarStream, FileName = avatarFileName!, FieldName = "avatar" },
                cancellationToken);
        }

        return GitLabContent.Wrap(ProjectMapper.ToSummary(updated), "projects/:id (update)");
    }

    // ---------------------------------------------------------------- gitlab_delete_project

    [McpServerTool(Name = "gitlab_delete_project", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a project, or schedules it for delayed deletion, depending on instance settings. Use gitlab_restore_project to undo a scheduled deletion before its delay window elapses.")]
    public async Task<ProjectDeleteResult> DeleteProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        await projects.DeleteAsync(project, cancellationToken);
        return new ProjectDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_restore_project

    [McpServerTool(Name = "gitlab_restore_project", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Restores a project that is still inside its scheduled-deletion delay window, undoing gitlab_delete_project before the delay elapses.")]
    public async Task<CallToolResult> RestoreProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var restored = await projects.RestoreAsync(project, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(restored), "projects/:id/restore");
    }

    // ---------------------------------------------------------------- gitlab_set_project_archived

    [McpServerTool(Name = "gitlab_set_project_archived", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Archives (makes read-only) or unarchives a project.")]
    public async Task<CallToolResult> SetProjectArchivedAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("True to archive the project (read-only); false to unarchive it.")]
        bool archived,
        CancellationToken cancellationToken = default)
    {
        var result = archived
            ? await projects.ArchiveAsync(project, cancellationToken)
            : await projects.UnarchiveAsync(project, cancellationToken);

        return GitLabContent.Wrap(ProjectMapper.ToSummary(result),
            archived ? "projects/:id/archive" : "projects/:id/unarchive");
    }

    // ---------------------------------------------------------------- gitlab_set_project_starred

    [McpServerTool(Name = "gitlab_set_project_starred", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Stars or unstars a project as the authenticated user.")]
    public async Task<CallToolResult> SetProjectStarredAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("True to star the project; false to unstar it.")]
        bool starred,
        CancellationToken cancellationToken = default)
    {
        var result = starred
            ? await projects.StarAsync(project, cancellationToken)
            : await projects.UnstarAsync(project, cancellationToken);

        return GitLabContent.Wrap(ProjectMapper.ToSummary(result),
            starred ? "projects/:id/star" : "projects/:id/unstar");
    }

    // ---------------------------------------------------------------- gitlab_fork_project

    [McpServerTool(Name = "gitlab_fork_project", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Forks a project into the caller's own namespace, or into a specified target namespace with an optional new name and path.")]
    public async Task<CallToolResult> ForkProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Numeric id of the target namespace (group) for the fork. Omit to fork into the caller's personal namespace. Mutually exclusive with namespacePath.")]
        long? namespaceId = null,
        [Description(
            "Full path of the target namespace, as an alternative to namespaceId. Mutually exclusive with namespaceId.")]
        string? namespacePath = null,
        [Description("New project name for the fork. Omit to keep the source project's name.")]
        string? name = null,
        [Description("New URL path segment for the fork. Omit to keep the source project's path.")]
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        if (namespaceId is not null && !string.IsNullOrEmpty(namespacePath))
            throw new McpException("Pass at most one of namespaceId or namespacePath.");

        var request = namespaceId is null && string.IsNullOrEmpty(namespacePath) && name is null && path is null
            ? null
            : new ForkProjectRequest
            {
                NamespaceId = namespaceId,
                NamespacePath = namespacePath,
                Name = name,
                Path = path
            };

        var fork = await projects.ForkAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(fork), "projects/:id/fork");
    }

    // ---------------------------------------------------------------- gitlab_transfer_project

    [McpServerTool(Name = "gitlab_transfer_project", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Moves a project to a different namespace. Omit targetNamespace to instead list candidate namespaces the project could be transferred into, without transferring anything.")]
    public async Task<CallToolResult> TransferProjectAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Full path of the destination namespace (group or user), e.g. \"my-group/subgroup\". Omit to list candidate namespaces instead of transferring.")]
        string? targetNamespace = null,
        [Description(
            "Only used when targetNamespace is omitted: maximum candidate namespaces to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (!string.IsNullOrEmpty(targetNamespace))
        {
            var transferred = await projects.TransferAsync(project,
                new TransferProjectRequest { Namespace = targetNamespace }, cancellationToken);
            return GitLabContent.Wrap(new ProjectTransferResult(ProjectMapper.ToSummary(transferred), [], false),
                "projects/:id/transfer");
        }

        var options = new ProjectTransferLocationListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<RelatedGroupSummary> collected = [];
        var truncated = false;

        await foreach (var location in projects.ListTransferLocationsAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToRelatedGroupSummary(location));
        }

        return GitLabContent.Wrap(new ProjectTransferResult(null, collected, truncated),
            "projects/:id/transfer_locations");
    }

    // ---------------------------------------------------------------- gitlab_share_project_with_group

    [McpServerTool(Name = "gitlab_share_project_with_group", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Grants a group access to a project, or revokes a previously granted share when accessLevel is omitted.")]
    public async Task<ProjectGroupShareResult> ShareProjectWithGroupAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Numeric id of the group to grant or revoke access for.")]
        long groupId,
        [Description(
            "Access level to grant, as GitLab's numeric role (10 Guest, 20 Reporter, 30 Developer, 40 Maintainer, 50 Owner). Omit to revoke the group's existing share instead of granting one.")]
        int? accessLevel = null,
        [Description(
            "Optional expiry date (yyyy-mm-dd) for the granted access. Only used when accessLevel is supplied.")]
        DateOnly? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (accessLevel is null)
        {
            await projects.UnshareAsync(project, groupId, cancellationToken);
            return new ProjectGroupShareResult(null, null, groupId, null, null, true);
        }

        var request = new ShareProjectRequest
            { GroupId = groupId, GroupAccess = accessLevel.Value, ExpiresAt = expiresAt };
        var link = await projects.ShareAsync(project, request, cancellationToken);

        return new ProjectGroupShareResult(link.Id, link.ProjectId, link.GroupId, link.GroupAccess, link.ExpiresAt,
            false);
    }

    // ---------------------------------------------------------------- gitlab_list_project_related_groups

    [McpServerTool(Name = "gitlab_list_project_related_groups", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists groups related to a project: its ancestor groups (own namespace and parents), the groups it has been shared with, or the groups it could still be shared with — selected by relation.")]
    public async Task<CallToolResult> ListProjectRelatedGroupsAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Which relation to list: \"ancestors\" (own namespace and parent groups), \"invited\" (groups this project has been shared with), or \"shareable\" (candidate groups for gitlab_share_project_with_group).")]
        string relation,
        [Description("Optional free-text search over group names and paths.")]
        string? search = null,
        [Description("Maximum groups to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var perPage = Math.Min(limit + 1, MaxLimit);
        var searchTerm = string.IsNullOrEmpty(search) ? null : search;
        List<RelatedGroupSummary> collected = [];
        var truncated = false;
        string source;

        switch (relation)
        {
            case "ancestors":
                source = "projects/:id/groups";
                var ancestorOptions = new ProjectAncestorGroupListOptions { Search = searchTerm, PerPage = perPage };
                await foreach (var g in projects.ListAncestorGroupsAsync(project, ancestorOptions, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToRelatedGroupSummary(g));
                }

                break;

            case "invited":
                source = "projects/:id/invited_groups";
                var invitedOptions = new ProjectInvitedGroupListOptions { Search = searchTerm, PerPage = perPage };
                await foreach (var g in projects.ListInvitedGroupsAsync(project, invitedOptions, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToRelatedGroupSummary(g));
                }

                break;

            case "shareable":
                source = "projects/:id/share_locations";
                var shareOptions = new ProjectShareLocationListOptions { Search = searchTerm };
                await foreach (var g in projects.ListShareLocationsAsync(project, shareOptions, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToRelatedGroupSummary(g));
                }

                break;

            default:
                throw new McpException("relation must be \"ancestors\", \"invited\", or \"shareable\".");
        }

        return GitLabContent.Wrap(new RelatedGroupListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_list_project_members

    [McpServerTool(Name = "gitlab_list_project_members", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists users associated with a project: its members plus, on public projects, everyone who can be @mentioned. Useful for assignee/reviewer lookups.")]
    public async Task<CallToolResult> ListProjectMembersAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Optional free-text search over usernames and names.")]
        string? search = null,
        [Description("Maximum users to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ProjectUserListOptions
            { Search = string.IsNullOrEmpty(search) ? null : search, PerPage = Math.Min(limit + 1, MaxLimit) };
        List<ProjectMemberSummary> collected = [];
        var truncated = false;

        await foreach (var user in projects.ListUsersAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToMemberSummary(user));
        }

        return GitLabContent.Wrap(new ProjectMemberListResult(collected, truncated), "projects/:id/users");
    }

    // ---------------------------------------------------------------- gitlab_get_project_insights

    [McpServerTool(Name = "gitlab_get_project_insights", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Combines a project's repository fetch statistics (last 30 days) and programming-language breakdown into one snapshot. With includeStorage, also fetches where its repository physically lives on Gitaly — that part requires an administrator token, and a non-admin token fails the whole call rather than returning a partial result.")]
    public async Task<CallToolResult> GetProjectInsightsAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "If true, also fetches the Gitaly storage location. Requires an administrator token. Default false.")]
        bool includeStorage = false,
        CancellationToken cancellationToken = default)
    {
        var statistics = await projects.GetStatisticsAsync(project, cancellationToken);
        var languages = await projects.GetLanguagesAsync(project, cancellationToken);

        string? diskPath = null;
        string? repositoryStorage = null;

        if (includeStorage)
        {
            var storage = await projects.GetStorageAsync(project, cancellationToken);
            diskPath = storage.DiskPath;
            repositoryStorage = storage.RepositoryStorage;
        }

        var result = new ProjectInsightsResult(
            statistics.Fetches?.Total,
            languages,
            diskPath,
            repositoryStorage);

        return GitLabContent.Wrap(result, "projects/:id/statistics");
    }

    // ---------------------------------------------------------------- gitlab_manage_project_security_settings

    [McpServerTool(Name = "gitlab_manage_project_security_settings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Reads a project's security settings (secret push protection, pre-receive secret detection), or applies changes when at least one setting is supplied.")]
    public async Task<ProjectSecuritySettingsResult> ManageProjectSecuritySettingsAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "Set to enable or disable secret push protection. Omit, together with preReceiveSecretDetectionEnabled, to just read the current settings instead of changing them.")]
        bool? secretPushProtectionEnabled = null,
        [Description("Set to enable or disable pre-receive secret detection. Omit to leave unchanged.")]
        bool? preReceiveSecretDetectionEnabled = null,
        CancellationToken cancellationToken = default)
    {
        GitLabProjectSecuritySettings settings;

        if (secretPushProtectionEnabled is null && preReceiveSecretDetectionEnabled is null)
        {
            settings = await projects.GetSecuritySettingsAsync(project, cancellationToken);
        }
        else
        {
            var request = new UpdateProjectSecuritySettingsRequest
            {
                SecretPushProtectionEnabled = secretPushProtectionEnabled,
                PreReceiveSecretDetectionEnabled = preReceiveSecretDetectionEnabled
            };

            settings = await projects.UpdateSecuritySettingsAsync(project, request, cancellationToken);
        }

        return new ProjectSecuritySettingsResult(
            settings.ProjectId,
            settings.CreatedAt,
            settings.UpdatedAt,
            settings.SecretPushProtectionEnabled,
            settings.PreReceiveSecretDetectionEnabled,
            settings.FastDependencyPathsEnabled,
            settings.ValidityChecksEnabled);
    }

    // ---------------------------------------------------------------- gitlab_list_user_projects

    [McpServerTool(Name = "gitlab_list_user_projects", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists projects associated with a given user, selected by relation: projects they own, have starred, or have contributed to.")]
    public async Task<CallToolResult> ListUserProjectsAsync(
        [Description("The user: numeric user id or username.")]
        string user,
        [Description("Which relation to list: \"owned\", \"starred\", or \"contributed\".")]
        string relation,
        [Description("Maximum projects to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ProjectListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<ProjectSummary> collected = [];
        var truncated = false;

        var stream = relation switch
        {
            "owned" => projects.ListForUserAsync(user, options, cancellationToken),
            "starred" => projects.ListStarredByUserAsync(user, options, cancellationToken),
            "contributed" => projects.ListContributedByUserAsync(user, options, cancellationToken),
            _ => throw new McpException("relation must be \"owned\", \"starred\", or \"contributed\".")
        };

        await foreach (var project in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(project));
        }

        var source = relation switch
        {
            "owned" => "users/:user_id/projects",
            "starred" => "users/:user_id/starred_projects",
            _ => "users/:user_id/contributed_projects"
        };

        return GitLabContent.Wrap(new ProjectListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_get_group

    [McpServerTool(Name = "gitlab_get_group", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one GitLab group's details by numeric id or full path.")]
    public async Task<CallToolResult> GetGroupAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        CancellationToken cancellationToken = default)
    {
        var result = await groups.GetAsync(group, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(result), "groups/:id");
    }

    // ---------------------------------------------------------------- gitlab_list_groups

    [McpServerTool(Name = "gitlab_list_groups", ReadOnly = true, OpenWorld = false)]
    [Description("Searches or browses GitLab groups visible to the configured token.")]
    public async Task<CallToolResult> ListGroupsAsync(
        [Description("Optional free-text search over group names and paths.")]
        string? search = null,
        [Description("Filter by visibility: \"private\", \"internal\", \"public\", or omit for all.")]
        string? visibility = null,
        [Description("Maximum groups to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new GroupListOptions
        {
            Search = string.IsNullOrEmpty(search) ? null : search,
            Visibility = ParseVisibility(visibility),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<GroupSummary> collected = [];
        var truncated = false;

        await foreach (var group in groups.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(group));
        }

        return GitLabContent.Wrap(new GroupListResult(collected, truncated), "groups");
    }

    // ================================================================================
    // Chunk 2 additions (backlog part-2.json): group lifecycle/sharing, group special-
    // user/audit reads, group security settings, project uploads, and namespaces.
    // ================================================================================

    // ---------------------------------------------------------------- gitlab_update_group

    [McpServerTool(Name = "gitlab_update_group", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a group's settings (name, description, visibility) and, when an avatar image is supplied, replaces its avatar in the same call. Only the fields you supply are changed.")]
    public async Task<CallToolResult> UpdateGroupAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("New group name. Omit to leave unchanged.")]
        string? name = null,
        [Description("New group description. Omit to leave unchanged.")]
        string? description = null,
        [Description("New visibility: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? visibility = null,
        [Description(
            "Base64-encoded avatar image to set as the group's new avatar. Requires avatarFileName. Omit to leave the avatar unchanged.")]
        string? avatarBase64 = null,
        [Description(
            "Filename for the avatar image, e.g. \"logo.png\". Required together with avatarBase64; ignored otherwise.")]
        string? avatarFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(avatarBase64) != string.IsNullOrEmpty(avatarFileName))
            throw new McpException("avatarBase64 and avatarFileName must be supplied together.");

        var request = new UpdateGroupRequest
        {
            Name = name,
            Description = description,
            Visibility = ParseVisibility(visibility)
        };

        var updated = await groups.UpdateAsync(group, request, cancellationToken);

        if (!string.IsNullOrEmpty(avatarBase64))
        {
            byte[] avatarBytes;
            try
            {
                avatarBytes = Convert.FromBase64String(avatarBase64);
            }
            catch (FormatException)
            {
                throw new McpException("avatarBase64 is not valid base64.");
            }

            await using var avatarStream = new MemoryStream(avatarBytes);
            updated = await groups.SetAvatarAsync(
                group,
                new GitLabFileUpload { Content = avatarStream, FileName = avatarFileName!, FieldName = "avatar" },
                cancellationToken);
        }

        return GitLabContent.Wrap(ProjectMapper.ToSummary(updated), "groups/:id (update)");
    }

    // ---------------------------------------------------------------- gitlab_delete_group

    [McpServerTool(Name = "gitlab_delete_group", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Schedules a group, and everything under it, for deletion. GitLab answers 202 Accepted and only marks the group; use gitlab_restore_group to undo this before its delay window elapses.")]
    public async Task<GroupDeleteResult> DeleteGroupAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        CancellationToken cancellationToken = default)
    {
        await groups.DeleteAsync(group, cancellationToken);
        return new GroupDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_restore_group

    [McpServerTool(Name = "gitlab_restore_group", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Cancels a pending group deletion, undoing gitlab_delete_group before its delay window elapses.")]
    public async Task<GroupRestoreResult> RestoreGroupAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        CancellationToken cancellationToken = default)
    {
        await groups.RestoreAsync(group, cancellationToken);
        return new GroupRestoreResult(true);
    }

    // ---------------------------------------------------------------- gitlab_set_group_archived

    [McpServerTool(Name = "gitlab_set_group_archived", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Archives (makes read-only) or unarchives a group.")]
    public async Task<CallToolResult> SetGroupArchivedAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("True to archive the group (read-only); false to unarchive it.")]
        bool archived,
        CancellationToken cancellationToken = default)
    {
        var result = archived
            ? await groups.ArchiveAsync(group, cancellationToken)
            : await groups.UnarchiveAsync(group, cancellationToken);

        return GitLabContent.Wrap(ProjectMapper.ToSummary(result),
            archived ? "groups/:id/archive" : "groups/:id/unarchive");
    }

    // ---------------------------------------------------------------- gitlab_list_subgroups

    [McpServerTool(Name = "gitlab_list_subgroups", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a group's direct child subgroups, or (with recursive=true) its whole subtree at any depth.")]
    public async Task<CallToolResult> ListSubgroupsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("If true, lists the entire subtree at any depth instead of only direct children. Default false.")]
        bool recursive = false,
        [Description("Optional free-text search over subgroup names and paths.")]
        string? search = null,
        [Description("Maximum subgroups to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new GroupHierarchyListOptions
        {
            Search = string.IsNullOrEmpty(search) ? null : search,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<GroupSummary> collected = [];
        var truncated = false;

        var stream = recursive
            ? groups.ListDescendantGroupsAsync(group, options, cancellationToken)
            : groups.ListSubgroupsAsync(group, options, cancellationToken);

        await foreach (var subgroup in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(subgroup));
        }

        var source = recursive ? "groups/:id/descendant_groups" : "groups/:id/subgroups";
        return GitLabContent.Wrap(new GroupListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_move_project_into_group

    [McpServerTool(Name = "gitlab_move_project_into_group", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Moves an existing project into this group. Distinct from gitlab_transfer_project, which moves a project to an arbitrary namespace path rather than into a group you already have the id of.")]
    public async Task<CallToolResult> MoveProjectIntoGroupAsync(
        [Description("The destination group's numeric id or full path.")]
        string group,
        [Description("The project to move: either its numeric id or the URL-path form \"group/subgroup/project\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var result = await groups.TransferProjectAsync(group, project, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(result), "groups/:id/projects/:project_id");
    }

    // ---------------------------------------------------------------- gitlab_set_group_share

    [McpServerTool(Name = "gitlab_set_group_share", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Grants another group access to this group's resources, or revokes a previously granted share when accessLevel is omitted.")]
    public async Task<CallToolResult> SetGroupShareAsync(
        [Description("The group whose resources are being shared: numeric id or full path.")]
        string group,
        [Description("Numeric id of the group to grant or revoke access for.")]
        long sharedWithGroupId,
        [Description(
            "Access level to grant, as GitLab's numeric role (10 Guest, 20 Reporter, 30 Developer, 40 Maintainer, 50 Owner). Omit to revoke the existing share instead of granting one.")]
        int? accessLevel = null,
        [Description(
            "Optional expiry date (yyyy-mm-dd) for the granted access. Only used when accessLevel is supplied.")]
        DateOnly? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (accessLevel is null)
        {
            await groups.UnshareAsync(group, sharedWithGroupId, cancellationToken);
            return GitLabContent.Wrap(new GroupShareResult(null, true), "groups/:id/share");
        }

        var request = new ShareGroupRequest
            { GroupId = sharedWithGroupId, GroupAccess = accessLevel.Value, ExpiresAt = expiresAt };
        var shared = await groups.ShareAsync(group, request, cancellationToken);

        return GitLabContent.Wrap(new GroupShareResult(ProjectMapper.ToSummary(shared), false), "groups/:id/share");
    }

    // ---------------------------------------------------------------- gitlab_transfer_group

    [McpServerTool(Name = "gitlab_transfer_group", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Moves a group under a new parent, or promotes it to top-level. If neither newParentGroupId nor promoteToTopLevel is given, lists candidate parent groups instead of transferring anything. Requires Owner access to this group and to the destination.")]
    public async Task<CallToolResult> TransferGroupAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description(
            "Numeric id of the new parent group. Mutually exclusive with promoteToTopLevel. Omit both to list candidate parents instead of transferring.")]
        long? newParentGroupId = null,
        [Description(
            "If true, promotes this group to a top-level group instead of moving it under a parent. Mutually exclusive with newParentGroupId.")]
        bool promoteToTopLevel = false,
        [Description(
            "Only used when neither newParentGroupId nor promoteToTopLevel is given: maximum candidate parents to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        if (newParentGroupId is not null && promoteToTopLevel)
            throw new McpException("Pass at most one of newParentGroupId or promoteToTopLevel.");

        if (newParentGroupId is not null || promoteToTopLevel)
        {
            await groups.TransferAsync(group, new TransferGroupRequest { GroupId = newParentGroupId },
                cancellationToken);
            var updated = await groups.GetAsync(group, cancellationToken);
            return GitLabContent.Wrap(new GroupTransferResult(ProjectMapper.ToSummary(updated), [], false),
                "groups/:id/transfer");
        }

        var options = new GroupTransferLocationListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
        List<RelatedGroupSummary> collected = [];
        var truncated = false;

        await foreach (var location in groups.ListTransferLocationsAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToRelatedGroupSummary(location));
        }

        return GitLabContent.Wrap(new GroupTransferResult(null, collected, truncated), "groups/:id/transfer_locations");
    }

    // ---------------------------------------------------------------- gitlab_get_group_issue_statistics

    [McpServerTool(Name = "gitlab_get_group_issue_statistics", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Counts a group's issues by state (open/closed/total), optionally filtered by the same free-text search as gitlab_list_group_issues.")]
    public async Task<GroupIssueStatisticsResult> GetGroupIssueStatisticsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Optional free-text search over issue titles and descriptions.")]
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var options = new GroupIssueStatisticsOptions { Search = string.IsNullOrEmpty(search) ? null : search };
        var statistics = await groups.GetIssueStatisticsAsync(group, options, cancellationToken);
        var counts = statistics.Statistics?.Counts;

        return new GroupIssueStatisticsResult(counts?.All, counts?.Opened, counts?.Closed);
    }

    // ---------------------------------------------------------------- gitlab_list_group_special_users

    [McpServerTool(Name = "gitlab_list_group_special_users", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists special user categories for a top-level group, selected by kind: billable-seat holders, SAML/SCIM-provisioned accounts, or everyone with a SAML identity in the group (a wider set than provisioned).")]
    public async Task<CallToolResult> ListGroupSpecialUsersAsync(
        [Description("The top-level group's numeric id or full path.")]
        string group,
        [Description(
            "Which category to list: \"billable\" (seat holders), \"provisioned\" (SAML/SCIM-provisioned accounts), or \"saml\" (everyone with a SAML identity).")]
        string kind,
        [Description("Optional free-text search over usernames and names.")]
        string? search = null,
        [Description("Maximum users to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var perPage = Math.Min(limit + 1, MaxLimit);
        var searchTerm = string.IsNullOrEmpty(search) ? null : search;
        List<GroupSpecialUserSummary> collected = [];
        var truncated = false;
        string source;

        switch (kind)
        {
            case "billable":
                source = "groups/:id/billable_members";
                var billableOptions = new GroupBillableMemberListOptions { Search = searchTerm, PerPage = perPage };
                await foreach (var member in groups.ListBillableMembersAsync(group, billableOptions, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToSummary(member));
                }

                break;

            case "provisioned":
                source = "groups/:id/provisioned_users";
                var provisionedOptions = new GroupUserListOptions { Search = searchTerm, PerPage = perPage };
                await foreach (var user in groups.ListProvisionedUsersAsync(group, provisionedOptions,
                                   cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToGroupSpecialUserSummary(user));
                }

                break;

            case "saml":
                source = "groups/:id/saml_users";
                var samlOptions = new GroupUserListOptions { Search = searchTerm, PerPage = perPage };
                await foreach (var user in groups.ListSamlUsersAsync(group, samlOptions, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(ProjectMapper.ToGroupSpecialUserSummary(user));
                }

                break;

            default:
                throw new McpException("kind must be \"billable\", \"provisioned\", or \"saml\".");
        }

        return GitLabContent.Wrap(new GroupSpecialUserListResult(collected, truncated), source);
    }

    // ---------------------------------------------------------------- gitlab_get_group_audit_event

    [McpServerTool(Name = "gitlab_get_group_audit_event", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Fetches one entry from a group's audit log by its id. No list endpoint exists on this client for groups; find ids via GitLab's own audit event UI or CSV export. Restricted to group Owners and administrators.")]
    public async Task<CallToolResult> GetGroupAuditEventAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("The audit event's numeric id.")]
        long auditEventId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = await groups.GetAuditEventAsync(group, auditEventId, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToResult(auditEvent), "groups/:id/audit_events/:audit_event_id");
    }

    // ---------------------------------------------------------------- gitlab_update_group_security_settings

    [McpServerTool(Name = "gitlab_update_group_security_settings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Updates a group's security settings, such as secret push protection. Requires the Security Manager, Maintainer, or Owner role.")]
    public async Task<GroupSecuritySettingsUpdateResult> UpdateGroupSecuritySettingsAsync(
        [Description("The group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Whether secret push protection should be enabled for this group.")]
        bool secretPushProtectionEnabled,
        [Description(
            "Comma-separated numeric project ids under this group to exclude from secret push protection. Omit for none.")]
        string? projectsToExcludeCsv = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateGroupSecuritySettingsRequest
        {
            SecretPushProtectionEnabled = secretPushProtectionEnabled,
            ProjectsToExclude = ParseLongList(projectsToExcludeCsv, nameof(projectsToExcludeCsv)) ?? []
        };

        await groups.UpdateSecuritySettingsAsync(group, request, cancellationToken);
        return new GroupSecuritySettingsUpdateResult(true, secretPushProtectionEnabled);
    }

    // ---------------------------------------------------------------- gitlab_upload_project_file

    [McpServerTool(Name = "gitlab_upload_project_file", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Uploads a file to a project's markdown-attachment store and returns the pastable markdown snippet that embeds it, for use in an issue, MR, or comment body.")]
    public async Task<CallToolResult> UploadProjectFileAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description("Base64-encoded content of the file to upload.")]
        string fileContentBase64,
        [Description("Filename for the upload, e.g. \"diagram.png\".")]
        string fileName,
        CancellationToken cancellationToken = default)
    {
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
        var upload = new GitLabFileUpload { Content = fileStream, FileName = fileName, FieldName = "file" };
        var link = await projectUploads.UploadAsync(project, upload, cancellationToken);

        return GitLabContent.Wrap(ProjectMapper.ToSummary(link), "projects/:id/uploads (create)");
    }

    // ---------------------------------------------------------------- gitlab_download_project_upload

    [McpServerTool(Name = "gitlab_download_project_upload", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads a project upload (a markdown attachment) by its numeric id, or by the secret+filename pair scraped from its markdown link. Returns the raw bytes base64-encoded. Refuses files over 4 MB rather than returning a truncated, unusable partial file.")]
    public async Task<CallToolResult> DownloadProjectUploadAsync(
        [Description("Project: either the numeric project id or the URL-path form \"group/subgroup/project\".")]
        string project,
        [Description(
            "The upload's numeric id (from gitlab_list_project_uploads). Mutually exclusive with secret/filename.")]
        long? uploadId = null,
        [Description(
            "The upload's secret path component, as embedded in its markdown URL. Requires filename too; mutually exclusive with uploadId. Needs only read access to the project, unlike uploadId.")]
        string? secret = null,
        [Description("The upload's original filename, paired with secret. Mutually exclusive with uploadId.")]
        string? filename = null,
        CancellationToken cancellationToken = default)
    {
        var bySecret = !string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(filename);

        if (uploadId is null == !bySecret)
            throw new McpException("Pass exactly one of uploadId, or the secret+filename pair.");

        await using var file = uploadId is not null
            ? await projectUploads.DownloadAsync(project, uploadId.Value, cancellationToken)
            : await projectUploads.DownloadBySecretAsync(project, secret!, filename!, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxProjectUploadDownloadBytes)
            throw new McpException(
                $"Upload is {declaredLength} bytes, over the {MaxProjectUploadDownloadBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var buffer = new byte[MaxProjectUploadDownloadBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxProjectUploadDownloadBytes)
            throw new McpException(
                $"Upload exceeds the {MaxProjectUploadDownloadBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var payload = new ProjectUploadDownloadResult(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/uploads (download)");
    }

    // ---------------------------------------------------------------- gitlab_get_namespace

    [McpServerTool(Name = "gitlab_get_namespace", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one namespace (a group, or a personal namespace) by numeric id or URL-encoded full path.")]
    public async Task<CallToolResult> GetNamespaceAsync(
        [Description(
            "The namespace's numeric id or full path, e.g. \"my-group\" or a username for a personal namespace.")]
        string namespaceId,
        CancellationToken cancellationToken = default)
    {
        var result = await namespaces.GetAsync(namespaceId, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(result), "namespaces/:id");
    }

    // ---------------------------------------------------------------- gitlab_list_namespaces

    [McpServerTool(Name = "gitlab_list_namespaces", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists namespaces visible to the configured token: every namespace on the instance for an administrator, or just the caller's own otherwise.")]
    public async Task<CallToolResult> ListNamespacesAsync(
        [Description("Optional free-text search over namespace names and paths.")]
        string? search = null,
        [Description("If true, return only namespaces the caller owns. Default false.")]
        bool ownedOnly = false,
        [Description("Maximum namespaces to return, 1-100. Default 20. The result reports whether more exist.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new NamespaceListOptions
        {
            Search = string.IsNullOrEmpty(search) ? null : search,
            OwnedOnly = ownedOnly ? true : null,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<NamespaceSummary> collected = [];
        var truncated = false;

        await foreach (var ns in namespaces.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProjectMapper.ToSummary(ns));
        }

        return GitLabContent.Wrap(new NamespaceListResult(collected, truncated), "namespaces");
    }

    // ---------------------------------------------------------------- gitlab_get_namespace_subscription

    [McpServerTool(Name = "gitlab_get_namespace_subscription", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Reads the GitLab subscription plan, seat usage and billing dates for a top-level namespace. Restricted to namespace Owners and administrators.")]
    public async Task<CallToolResult> GetNamespaceSubscriptionAsync(
        [Description("The top-level namespace's numeric id or full path.")]
        string namespaceId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await namespaces.GetSubscriptionAsync(namespaceId, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToResult(subscription), "namespaces/:id/gitlab_subscription");
    }

    // ---------------------------------------------------------------- gitlab_set_namespace_storage_limit_exclusion

    [McpServerTool(Name = "gitlab_set_namespace_storage_limit_exclusion", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Excludes a namespace from storage-limit enforcement, or removes an existing exclusion when reason is omitted. Administrator token required.")]
    public async Task<CallToolResult> SetNamespaceStorageLimitExclusionAsync(
        [Description("The namespace's numeric id or full path.")]
        string namespaceId,
        [Description(
            "Why this namespace is excluded. Supplying this creates or updates the exclusion; omit to remove an existing exclusion instead.")]
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(reason))
        {
            var request = new CreateNamespaceStorageLimitExclusionRequest { Reason = reason };
            var exclusion = await namespaces.CreateStorageLimitExclusionAsync(namespaceId, request, cancellationToken);
            return GitLabContent.Wrap(ProjectMapper.ToSummary(exclusion),
                "namespaces/:id/storage/limit_exclusion (create)");
        }

        await namespaces.DeleteStorageLimitExclusionAsync(namespaceId, cancellationToken);
        return GitLabContent.Wrap(new NamespaceStorageLimitExclusionDeleteResult(true),
            "namespaces/:id/storage/limit_exclusion (delete)");
    }

    // ================================================================================
    // Chunk 3 additions (backlog part-3.json): project aliases (get/delete), topics
    // (get/upsert/merge), badges (get/update/delete), custom attributes (get/delete),
    // avatar download, storage-move creation, and organizations (create/delete).
    // ================================================================================

    // ---------------------------------------------------------------- gitlab_get_project_alias

    [McpServerTool(Name = "gitlab_get_project_alias", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Fetches one Git-level project alias by its name (the alternate clone path GitLab answers to for a project). Premium/Ultimate, administrators only.")]
    public async Task<CallToolResult> GetProjectAliasAsync(
        [Description("The alias name to look up, not the project it points to.")]
        string aliasName,
        CancellationToken cancellationToken = default)
    {
        var alias = await projectAliases.GetAsync(aliasName, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(alias), "project_aliases/:name");
    }

    // ---------------------------------------------------------------- gitlab_delete_project_alias

    [McpServerTool(Name = "gitlab_delete_project_alias", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a Git-level project alias by name; the project it pointed at is untouched. Premium/Ultimate, administrators only. This cannot be undone.")]
    public async Task<ProjectAliasDeleteResult> DeleteProjectAliasAsync(
        [Description("The alias name to delete.")]
        string aliasName,
        CancellationToken cancellationToken = default)
    {
        await projectAliases.DeleteAsync(aliasName, cancellationToken);
        return new ProjectAliasDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_get_topic

    [McpServerTool(Name = "gitlab_get_topic", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one project topic by its numeric id. Open to any authenticated user.")]
    public async Task<CallToolResult> GetTopicAsync(
        [Description("The topic's numeric id (from gitlab_list_topics).")]
        long topicId,
        CancellationToken cancellationToken = default)
    {
        var topic = await topics.GetAsync(topicId, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(topic), "topics/:id");
    }

    // ---------------------------------------------------------------- gitlab_upsert_topic

    [McpServerTool(Name = "gitlab_upsert_topic", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new project topic, or updates an existing one's slug/title/description (and, when an avatar image is supplied, its avatar too) when topicId is given. Administrators only.")]
    public async Task<CallToolResult> UpsertTopicAsync(
        [Description(
            "The topic's short slug name. Required when creating (topicId omitted); when updating, supplying it renames the topic.")]
        string? name = null,
        [Description(
            "Numeric id of an existing topic (from gitlab_list_topics) to update instead of creating a new one.")]
        long? topicId = null,
        [Description("Display title for the topic. Omit to leave unchanged when updating.")]
        string? title = null,
        [Description("Optional topic description. Omit to leave unchanged when updating.")]
        string? description = null,
        [Description("Base64-encoded avatar image to set as the topic's avatar. Requires avatarFileName.")]
        string? avatarBase64 = null,
        [Description(
            "Filename for the avatar image, e.g. \"logo.png\". Required together with avatarBase64; ignored otherwise.")]
        string? avatarFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (topicId is null && string.IsNullOrEmpty(name))
            throw new McpException("name is required when creating a topic (topicId omitted).");

        if (topicId is null && string.IsNullOrEmpty(title))
            throw new McpException(
                "title is required when creating a topic (topicId omitted); GitLab requires it on create, unlike on update.");

        if (string.IsNullOrEmpty(avatarBase64) != string.IsNullOrEmpty(avatarFileName))
            throw new McpException("avatarBase64 and avatarFileName must be supplied together.");

        GitLabTopic topic;
        string source;

        if (topicId is { } id)
        {
            var request = new UpdateTopicRequest { Name = name, Title = title, Description = description };
            topic = await topics.UpdateAsync(id, request, cancellationToken);
            source = "topics/:id (update)";
        }
        else
        {
            var request = new CreateTopicRequest { Name = name!, Title = title!, Description = description };
            topic = await topics.CreateAsync(request, cancellationToken);
            source = "topics (create)";
        }

        if (!string.IsNullOrEmpty(avatarBase64))
        {
            byte[] avatarBytes;
            try
            {
                avatarBytes = Convert.FromBase64String(avatarBase64);
            }
            catch (FormatException)
            {
                throw new McpException("avatarBase64 is not valid base64.");
            }

            await using var avatarStream = new MemoryStream(avatarBytes);
            topic = await topics.SetAvatarAsync(
                topic.Id,
                new GitLabFileUpload { Content = avatarStream, FileName = avatarFileName!, FieldName = "avatar" },
                cancellationToken);
            source += "+avatar";
        }

        return GitLabContent.Wrap(ProjectMapper.ToSummary(topic), source);
    }

    // ---------------------------------------------------------------- gitlab_merge_topics

    [McpServerTool(Name = "gitlab_merge_topics", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Merges a duplicate topic into another: every project on the source topic moves to the target topic and the source topic is deleted. Administrators only. This cannot be undone.")]
    public async Task<CallToolResult> MergeTopicsAsync(
        [Description("Numeric id of the topic to merge away. It is deleted once the merge completes.")]
        long sourceTopicId,
        [Description("Numeric id of the topic that survives the merge and gains the source topic's projects.")]
        long targetTopicId,
        CancellationToken cancellationToken = default)
    {
        var request = new MergeTopicsRequest { SourceTopicId = sourceTopicId, TargetTopicId = targetTopicId };
        var merged = await topics.MergeAsync(request, cancellationToken);
        return GitLabContent.Wrap(ProjectMapper.ToSummary(merged), "topics/merge");
    }

    // ---------------------------------------------------------------- gitlab_get_badge

    [McpServerTool(Name = "gitlab_get_badge", ReadOnly = true, OpenWorld = false)]
    [Description("Fetches one project or group badge by its numeric id. Pass exactly one of project or group.")]
    public async Task<CallToolResult> GetBadgeAsync(
        [Description("Numeric id of the badge (from gitlab_list_badges).")]
        long badgeId,
        [Description(
            "Project the badge belongs to: numeric id or \"group/subgroup/project\". Mutually exclusive with group.")]
        string? project = null,
        [Description("Group the badge belongs to: numeric id or \"group/subgroup\". Mutually exclusive with project.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(project) == string.IsNullOrEmpty(group))
            throw new McpException("Pass exactly one of project or group.");

        var badge = !string.IsNullOrEmpty(project)
            ? await badges.GetForProjectAsync(project, badgeId, cancellationToken)
            : await badges.GetForGroupAsync(group!, badgeId, cancellationToken);

        var source = !string.IsNullOrEmpty(project) ? "projects/:id/badges/:badge_id" : "groups/:id/badges/:badge_id";
        return GitLabContent.Wrap(ProjectMapper.ToSummary(badge), source);
    }

    // ---------------------------------------------------------------- gitlab_update_badge

    [McpServerTool(Name = "gitlab_update_badge", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a project's or a group's badge: its link URL, image URL and/or display name. Only the fields you supply are changed. Pass exactly one of project or group.")]
    public async Task<CallToolResult> UpdateBadgeAsync(
        [Description("Numeric id of the badge to update (from gitlab_list_badges).")]
        long badgeId,
        [Description(
            "New link URL. May contain GitLab placeholders: %{project_path}, %{default_branch}, %{commit_sha}. Omit to leave unchanged.")]
        string? linkUrl = null,
        [Description(
            "New badge image source URL. May contain the same GitLab placeholders as linkUrl. Omit to leave unchanged.")]
        string? imageUrl = null,
        [Description("New display name for the badge. Omit to leave unchanged.")]
        string? name = null,
        [Description(
            "Project the badge belongs to: numeric id or \"group/subgroup/project\". Mutually exclusive with group.")]
        string? project = null,
        [Description("Group the badge belongs to: numeric id or \"group/subgroup\". Mutually exclusive with project.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(project) == string.IsNullOrEmpty(group))
            throw new McpException("Pass exactly one of project or group.");

        var request = new UpdateBadgeRequest { LinkUrl = linkUrl, ImageUrl = imageUrl, Name = name };

        var badge = !string.IsNullOrEmpty(project)
            ? await badges.UpdateForProjectAsync(project, badgeId, request, cancellationToken)
            : await badges.UpdateForGroupAsync(group!, badgeId, request, cancellationToken);

        var source = !string.IsNullOrEmpty(project)
            ? "projects/:id/badges/:badge_id (update)"
            : "groups/:id/badges/:badge_id (update)";
        return GitLabContent.Wrap(ProjectMapper.ToSummary(badge), source);
    }

    // ---------------------------------------------------------------- gitlab_delete_badge

    [McpServerTool(Name = "gitlab_delete_badge", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes a badge from a project or a group. Pass exactly one of project or group. This cannot be undone.")]
    public async Task<BadgeDeleteResult> DeleteBadgeAsync(
        [Description("Numeric id of the badge to remove (from gitlab_list_badges).")]
        long badgeId,
        [Description(
            "Project the badge belongs to: numeric id or \"group/subgroup/project\". Mutually exclusive with group.")]
        string? project = null,
        [Description("Group the badge belongs to: numeric id or \"group/subgroup\". Mutually exclusive with project.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(project) == string.IsNullOrEmpty(group))
            throw new McpException("Pass exactly one of project or group.");

        if (!string.IsNullOrEmpty(project))
            await badges.DeleteForProjectAsync(project, badgeId, cancellationToken);
        else
            await badges.DeleteForGroupAsync(group!, badgeId, cancellationToken);

        return new BadgeDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_get_custom_attribute

    [McpServerTool(Name = "gitlab_get_custom_attribute", ReadOnly = true, OpenWorld = false)]
    [Description("Reads one custom key/value attribute from a user, group, or project. Administrator token required.")]
    public async Task<CallToolResult> GetCustomAttributeAsync(
        [Description("Which kind of entity id identifies: \"user\", \"group\", or \"project\".")]
        string entityType,
        [Description(
            "The entity's id: a numeric user id for \"user\"; a numeric id or full path for \"group\"/\"project\".")]
        string id,
        [Description("The attribute's key.")] string key,
        CancellationToken cancellationToken = default)
    {
        var attribute = entityType switch
        {
            "user" => await customAttributes.GetForUserAsync(ParseUserId(id), key, cancellationToken),
            "group" => await customAttributes.GetForGroupAsync(id, key, cancellationToken),
            "project" => await customAttributes.GetForProjectAsync(id, key, cancellationToken),
            _ => throw new McpException("entityType must be \"user\", \"group\", or \"project\".")
        };

        return GitLabContent.Wrap(ProjectMapper.ToSummary(attribute), $"{entityType}s/:id/custom_attributes/:key");
    }

    // ---------------------------------------------------------------- gitlab_delete_custom_attribute

    [McpServerTool(Name = "gitlab_delete_custom_attribute", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a custom key/value attribute from a user, group, or project. Administrator token required. This cannot be undone.")]
    public async Task<CustomAttributeDeleteResult> DeleteCustomAttributeAsync(
        [Description("Which kind of entity id identifies: \"user\", \"group\", or \"project\".")]
        string entityType,
        [Description(
            "The entity's id: a numeric user id for \"user\"; a numeric id or full path for \"group\"/\"project\".")]
        string id,
        [Description("The attribute's key to delete.")]
        string key,
        CancellationToken cancellationToken = default)
    {
        switch (entityType)
        {
            case "user":
                await customAttributes.DeleteForUserAsync(ParseUserId(id), key, cancellationToken);
                break;

            case "group":
                await customAttributes.DeleteForGroupAsync(id, key, cancellationToken);
                break;

            case "project":
                await customAttributes.DeleteForProjectAsync(id, key, cancellationToken);
                break;

            default:
                throw new McpException("entityType must be \"user\", \"group\", or \"project\".");
        }

        return new CustomAttributeDeleteResult(true);
    }

    // ---------------------------------------------------------------- gitlab_download_avatar

    [McpServerTool(Name = "gitlab_download_avatar", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Downloads a project's or a group's avatar image and returns the raw bytes base64-encoded. Public projects/groups answer without a credential. An entity with no avatar of its own answers not found rather than the generated identicon GitLab's web UI falls back to. Refuses images over 2 MB rather than returning a truncated, unusable partial file.")]
    public async Task<CallToolResult> DownloadAvatarAsync(
        [Description("Which kind of entity id identifies: \"project\" or \"group\".")]
        string entityType,
        [Description(
            "The entity's id: numeric id or \"group/subgroup/project\" path for \"project\"; numeric id or full path for \"group\".")]
        string id,
        CancellationToken cancellationToken = default)
    {
        await using var file = entityType switch
        {
            "project" => await avatars.DownloadForProjectAsync(id, cancellationToken),
            "group" => await avatars.DownloadForGroupAsync(id, cancellationToken),
            _ => throw new McpException("entityType must be \"project\" or \"group\".")
        };

        if (file.ContentLength is { } declaredLength && declaredLength > MaxAvatarDownloadBytes)
            throw new McpException(
                $"Avatar is {declaredLength} bytes, over the {MaxAvatarDownloadBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var buffer = new byte[MaxAvatarDownloadBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxAvatarDownloadBytes)
            throw new McpException(
                $"Avatar exceeds the {MaxAvatarDownloadBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var payload = new AvatarDownloadResult(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        var source = entityType == "project" ? "projects/:id/avatar" : "groups/:id/avatar";
        return GitLabContent.Wrap(payload, source);
    }

    // ---------------------------------------------------------------- gitlab_create_storage_move

    [McpServerTool(Name = "gitlab_create_storage_move", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Schedules a Gitaly repository-storage move for one project, one group's wiki, or one snippet, selected by entityType. Administrator token required; this resource is unusable against GitLab.com and only meaningful on a self-managed instance.")]
    public async Task<CallToolResult> CreateStorageMoveAsync(
        [Description(
            "Which kind of entity to move: \"project\" (its repository, wiki and design repositories), \"group\" (only its wiki repository, not the projects inside it), or \"snippet\".")]
        string entityType,
        [Description(
            "The entity's id: numeric id or \"group/subgroup/project\" path for \"project\"; numeric id or full path for \"group\"; a numeric snippet id for \"snippet\".")]
        string id,
        [Description(
            "Name of the destination Gitaly storage shard to move the repository to. Omit to let GitLab pick a destination shard automatically.")]
        string? destinationStorageName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateStorageMoveRequest { DestinationStorageName = destinationStorageName };
        StorageMoveCreateResult result;
        string source;

        switch (entityType)
        {
            case "project":
                var projectMove = await storageMoves.CreateForProjectAsync(id, request, cancellationToken);
                result = ProjectMapper.ToCreateResult(projectMove);
                source = "projects/:id/repository_storage_moves (create)";
                break;

            case "group":
                var groupMove = await storageMoves.CreateForGroupAsync(id, request, cancellationToken);
                result = ProjectMapper.ToCreateResult(groupMove);
                source = "groups/:id/repository_storage_moves (create)";
                break;

            case "snippet":
                if (!long.TryParse(id, out var snippetId))
                    throw new McpException("id must be a numeric snippet id when entityType is \"snippet\".");

                var snippetMove = await storageMoves.CreateForSnippetAsync(snippetId, request, cancellationToken);
                result = ProjectMapper.ToCreateResult(snippetMove);
                source = "snippets/:id/repository_storage_moves (create)";
                break;

            default:
                throw new McpException("entityType must be \"project\", \"group\", or \"snippet\".");
        }

        return GitLabContent.Wrap(result, source);
    }

    // ---------------------------------------------------------------- gitlab_create_organization

    [McpServerTool(Name = "gitlab_create_organization", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a top-level organization (GitLab 17.5+, still experimental behind a feature flag), optionally with an avatar image attached in the same call.")]
    public async Task<CallToolResult> CreateOrganizationAsync(
        [Description("The organization's display name.")]
        string name,
        [Description("The organization's URL path segment (lowercase, no spaces).")]
        string path,
        [Description("Optional organization description.")]
        string? description = null,
        [Description(
            "Visibility: \"private\" or \"public\" (organizations have no \"internal\" level). Omit for GitLab's default.")]
        string? visibility = null,
        [Description(
            "Base64-encoded avatar image to attach to the new organization. Requires avatarFileName. Omit for no avatar.")]
        string? avatarBase64 = null,
        [Description(
            "Filename for the avatar image, e.g. \"logo.png\". Required together with avatarBase64; ignored otherwise.")]
        string? avatarFileName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(avatarBase64) != string.IsNullOrEmpty(avatarFileName))
            throw new McpException("avatarBase64 and avatarFileName must be supplied together.");

        var request = new CreateOrganizationRequest
        {
            Name = name,
            Path = path,
            Description = description,
            Visibility = ParseOrganizationVisibility(visibility)
        };

        GitLabFileUpload? avatar = null;
        MemoryStream? avatarStream = null;

        if (!string.IsNullOrEmpty(avatarBase64))
        {
            byte[] avatarBytes;
            try
            {
                avatarBytes = Convert.FromBase64String(avatarBase64);
            }
            catch (FormatException)
            {
                throw new McpException("avatarBase64 is not valid base64.");
            }

            avatarStream = new MemoryStream(avatarBytes);
            avatar = new GitLabFileUpload { Content = avatarStream, FileName = avatarFileName!, FieldName = "avatar" };
        }

        try
        {
            var organization = await organizations.CreateAsync(request, avatar, cancellationToken);
            return GitLabContent.Wrap(ProjectMapper.ToSummary(organization), "organizations (create)");
        }
        finally
        {
            if (avatarStream is not null) await avatarStream.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------- gitlab_delete_organization

    [McpServerTool(Name = "gitlab_delete_organization", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Soft-deletes a top-level organization (GitLab 17.5+, still experimental). GitLab answers 202 Accepted; no restore endpoint exists yet, so this cannot be undone.")]
    public async Task<OrganizationDeleteResult> DeleteOrganizationAsync(
        [Description("The organization's numeric id.")]
        long organizationId,
        CancellationToken cancellationToken = default)
    {
        await organizations.DeleteAsync(organizationId, cancellationToken);
        return new OrganizationDeleteResult(true);
    }

    private static long ParseUserId(string id)
    {
        return long.TryParse(id, out var userId)
            ? userId
            : throw new McpException("id must be a numeric user id when entityType is \"user\".");
    }

    private static GitLabVisibility? ParseVisibility(string? visibility)
    {
        return visibility?.ToLowerInvariant() switch
        {
            "private" => GitLabVisibility.Private,
            "internal" => GitLabVisibility.Internal,
            "public" => GitLabVisibility.Public,
            null or "" => null,
            _ => throw new McpException("visibility must be \"private\", \"internal\", \"public\", or omitted.")
        };
    }

    private static GitLabOrganizationVisibility? ParseOrganizationVisibility(string? visibility)
    {
        return visibility?.ToLowerInvariant() switch
        {
            "private" => GitLabOrganizationVisibility.Private,
            "public" => GitLabOrganizationVisibility.Public,
            null or "" => null,
            _ => throw new McpException("visibility must be \"private\", \"public\", or omitted.")
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
}