using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using GitLab.Client.Abstractions;
using GitLab.Client.Abstractions.Exceptions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Deploy;
using GitlabMCP.Mapping.Deploy;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Deploy-domain tools: runners, runner controllers, environments, protected environments,
///     deployments, releases, deploy keys/tokens, Pages domains and secure files. Built directly on
///     <c>GitLab.Client</c>. Every tool returning GitLab-authored text wraps via <see cref="GitLabContent" />;
///     the all-scalar exceptions (scope/delete/reset confirmations such as
///     <see cref="ListRunnerControllerScopesAsync" />, <see cref="DeleteRunnerAsync" />,
///     <see cref="ResetRunnerRegistrationTokenAsync" />) declare their bare projection record instead.
/// </summary>
[McpServerToolType]
public sealed class DeployTools(
    IReleasesClient releases,
    IRunnersClient runners,
    IPagesClient pages,
    IRunnerControllersClient runnerControllers,
    IDeploymentsClient deployments,
    IDeployKeysClient deployKeys,
    IDeployTokensClient deployTokens,
    ISecureFilesClient secureFiles,
    IEnvironmentsClient environments,
    IProtectedEnvironmentsClient protectedEnvironments,
    IFreezePeriodsClient freezePeriods)
{
    private const int MaxLimit = 100;

    /// <summary>
    ///     Cap for a single-file transfer carried base64-encoded in a tool payload (release assets, secure
    ///     file uploads/downloads) — matches <c>PackagesTools</c>' generic-package-file cap and
    ///     <c>CicdTools</c>' single-artifact-file cap. Refused rather than truncated in both directions: a
    ///     partial certificate, provisioning profile or keystore is unusable, and a partial binary release
    ///     asset is worse than none.
    /// </summary>
    private const int MaxFileBytes = 4 * 1024 * 1024;

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_group_releases
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_releases", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists releases across every project in a GitLab group, ordered by release date.")]
    public async Task<CallToolResult> ListGroupReleasesAsync(
        [Description("Group: numeric id or \"namespace/path\", e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Sort order by release date: \"asc\" or \"desc\". Omit for GitLab's default (\"desc\").")]
        string? sort = null,
        [Description("If true, GitLab returns a lighter, limited set of fields per release. Default false.")]
        bool simple = false,
        [Description("Maximum releases to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateSort(sort);

        var options = new GroupReleaseListOptions
        {
            Sort = string.IsNullOrEmpty(sort) ? null : sort,
            Simple = simple ? true : null,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<ReleaseSummary> collected = [];
        var truncated = false;
        await foreach (var release in releases.ListForGroupAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ReleaseMapper.ToSummary(release));
        }

        return GitLabContent.Wrap(new ReleaseListResult(collected, truncated), "groups/:id/releases");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_admin_runners
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_admin_runners", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every runner registered on the whole GitLab instance, for fleet-wide capacity or compliance review. Requires administrator or auditor access; a lower-privileged token gets a gitlab_forbidden error.")]
    public async Task<CallToolResult> ListAdminRunnersAsync(
        [Description("Filter by runner scope: \"instance_type\", \"group_type\", or \"project_type\". Omit for all.")]
        string? type = null,
        [Description(
            "Filter by status: \"active\", \"paused\", \"online\", \"offline\", \"never_contacted\", or \"stale\". Omit for all.")]
        string? status = null,
        [Description(
            "If true, return only runners currently ignoring new jobs; if false, only ones accepting jobs. Omit for both.")]
        bool? paused = null,
        [Description(
            "Comma-separated tags; only runners carrying every listed tag are returned. Omit for no tag filter.")]
        string? tags = null,
        [Description("Maximum runners to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateRunnerType(type);
        ValidateRunnerStatus(status);

        var options = new RunnerListOptions
        {
            Type = string.IsNullOrEmpty(type) ? null : type,
            Status = string.IsNullOrEmpty(status) ? null : status,
            Paused = paused,
            TagList = ParseCommaList(tags),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<RunnerSummary> collected = [];
        var truncated = false;
        await foreach (var runner in runners.ListAllAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerMapper.ToSummary(runner));
        }

        return GitLabContent.Wrap(new RunnerListResult(collected, truncated), "runners/all");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_instance_pages_domains
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_instance_pages_domains", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every GitLab Pages domain on the whole instance, with its owning project id and whether its TLS certificate has expired. Never returns the certificate itself. Requires administrator access.")]
    public async Task<CallToolResult> ListInstancePagesDomainsAsync(
        [Description("Restrict the result to one exact hostname. Omit to list every domain on the instance.")]
        string? domain = null,
        [Description("Maximum domains to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<PagesDomainSummary> collected = [];
        var truncated = false;
        await foreach (var d in pages.ListAllDomainsAsync(string.IsNullOrWhiteSpace(domain) ? null : domain,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PagesMapper.ToSummary(d));
        }

        return GitLabContent.Wrap(new PagesDomainListResult(collected, truncated), "pages/domains");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_controller_scopes
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_controller_scopes", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists what a runner controller currently governs: the whole instance (reported as a null runner id) or a specific set of runners it has been scoped to. Requires administrator access.")]
    public async Task<RunnerControllerScopeListResult> ListRunnerControllerScopesAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("Maximum scopes to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<RunnerControllerScopeSummary> collected = [];
        var truncated = false;
        await foreach (var scope in runnerControllers.ListScopesAsync(runnerControllerId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerControllerMapper.ToScopeSummary(scope));
        }

        return new RunnerControllerScopeListResult(collected, truncated);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_deployment_merge_requests
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_deployment_merge_requests", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Lists the merge requests GitLab associates with one deployment, to see what code shipped in it.")]
    public async Task<CallToolResult> ListDeploymentMergeRequestsAsync(
        [Description("Project: numeric id or \"namespace/path\", e.g. \"42\" or \"my-group/my-project\".")]
        string project,
        [Description("The deployment's numeric id.")]
        long deploymentId,
        [Description("Maximum merge requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new MergeRequestListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<DeploymentMergeRequestSummary> collected = [];
        var truncated = false;
        await foreach (var mergeRequest in deployments.ListMergeRequestsAsync(project, deploymentId, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeploymentMapper.ToSummary(mergeRequest));
        }

        return GitLabContent.Wrap(new DeploymentMergeRequestListResult(collected, truncated),
            "projects/:id/deployments/:id/merge_requests");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_admin_deploy_keys
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_admin_deploy_keys", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every deploy key on the instance, or - given a user id - every project deploy key accessible to that user across every project they can access. Requires administrator access.")]
    public async Task<CallToolResult> ListAdminDeployKeysAsync(
        [Description(
            "If set, list this user's accessible project deploy keys instead of every instance deploy key. Numeric user id.")]
        long? userId = null,
        [Description("Only used when userId is omitted: if true, return only public deploy keys. Omit for all.")]
        bool? publicOnly = null,
        [Description("Maximum deploy keys to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var source = userId is long uid
            ? deployKeys.ListForUserAsync(uid,
                new UserProjectDeployKeyListOptions { PerPage = Math.Min(limit + 1, MaxLimit) }, cancellationToken)
            : deployKeys.ListAllAsync(publicOnly, cancellationToken);

        List<DeployKeySummary> collected = [];
        var truncated = false;
        await foreach (var key in source)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeployKeyMapper.ToSummary(key));
        }

        return GitLabContent.Wrap(new DeployKeyListResult(collected, truncated), "deploy_keys");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_admin_deploy_tokens
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_admin_deploy_tokens", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every deploy token on the instance. Never returns a usable token value - only metadata. Requires administrator access.")]
    public async Task<CallToolResult> ListAdminDeployTokensAsync(
        [Description("If true, return only active tokens; if false, only revoked or expired ones. Omit for all.")]
        bool? active = null,
        [Description("Maximum deploy tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new DeployTokenListOptions { Active = active, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<DeployTokenSummary> collected = [];
        var truncated = false;
        await foreach (var token in deployTokens.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeployTokenMapper.ToSummary(token));
        }

        return GitLabContent.Wrap(new DeployTokenListResult(collected, truncated), "deploy_tokens");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_secure_files
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_secure_files", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's secure files (CI/CD signing certificates, provisioning profiles, and similar) by metadata only - never the file contents.")]
    public async Task<CallToolResult> ListSecureFilesAsync(
        [Description("Project: numeric id or \"namespace/path\", e.g. \"42\" or \"my-group/my-project\".")]
        string project,
        [Description("Maximum secure files to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<SecureFileSummary> collected = [];
        var truncated = false;
        await foreach (var file in secureFiles.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(SecureFileMapper.ToSummary(file));
        }

        return GitLabContent.Wrap(new SecureFileListResult(collected, truncated), "projects/:id/secure_files");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_runner_controller_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_runner_controller_token", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a new token record for a runner controller. GitLab returns only the token's metadata, never a usable secret - rotate the token afterward to obtain one.")]
    public async Task<CallToolResult> CreateRunnerControllerTokenAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("Optional free-text description to label this token. Omit for none.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateRunnerControllerTokenRequest { Description = description };
        var token = await runnerControllers.CreateTokenAsync(runnerControllerId, request, cancellationToken);
        return GitLabContent.Wrap(RunnerControllerMapper.ToTokenSummary(token),
            "runner_controllers/:id/tokens (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_release
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_release", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Cuts a new release from a tag, with notes and optionally linked milestones. If the tag does not already exist, GitLab creates it from commitRef.")]
    public async Task<CallToolResult> CreateReleaseAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The Git tag this release is attached to.")]
        string tagName,
        [Description(
            "The branch name or commit SHA to create the tag from, if tagName does not already exist. Omit if the tag already exists.")]
        string? commitRef = null,
        [Description("Optional release title. Defaults to the tag name if omitted.")]
        string? name = null,
        [Description("Optional release notes, in GitLab-flavored Markdown.")]
        string? description = null,
        [Description("Optional release date/time, ISO 8601 (e.g. \"2026-09-09T00:00:00Z\"). Defaults to now.")]
        string? releasedAt = null,
        [Description("Optional comma-separated milestone titles to link to this release.")]
        string? milestones = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateReleaseRequest
        {
            TagName = tagName,
            Ref = commitRef,
            Name = name,
            Description = description,
            ReleasedAt = ParseTimestamp(releasedAt, nameof(releasedAt)),
            Milestones = ParseCommaList(milestones)
        };

        var release = await releases.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToSummary(release), "projects/:id/releases (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_runner
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_runner", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a runner's description, tags, pause state, lock or timeout without recreating it. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateRunnerAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        [Description("New description, or omit to leave unchanged.")]
        string? description = null,
        [Description(
            "If true, the runner stops accepting new jobs; if false, it resumes accepting them. Omit to leave unchanged.")]
        bool? paused = null,
        [Description("Comma-separated tags to set on the runner, replacing its current tags. Omit to leave unchanged.")]
        string? tags = null,
        [Description("If true, the runner also picks up untagged jobs. Omit to leave unchanged.")]
        bool? runUntagged = null,
        [Description(
            "If true, the runner's tags and behavior can no longer be edited from other projects. Omit to leave unchanged.")]
        bool? locked = null,
        [Description("\"not_protected\" or \"ref_protected\". Omit to leave unchanged.")]
        string? accessLevel = null,
        [Description("Maximum job timeout in seconds this runner will accept. Omit to leave unchanged.")]
        int? maximumTimeout = null,
        [Description("New maintenance note, or omit to leave unchanged.")]
        string? maintenanceNote = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAccessLevel(accessLevel);

        var request = new UpdateRunnerRequest
        {
            Description = description,
            Paused = paused,
            TagList = ParseCommaList(tags),
            RunUntagged = runUntagged,
            Locked = locked,
            AccessLevel = string.IsNullOrEmpty(accessLevel) ? null : accessLevel,
            MaximumTimeout = maximumTimeout,
            MaintenanceNote = maintenanceNote
        };

        var runner = await runners.UpdateAsync(runnerId, request, cancellationToken);
        return GitLabContent.Wrap(RunnerMapper.ToSummary(runner), "runners/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_environment", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Registers a new deployment environment on a project.")]
    public async Task<CallToolResult> CreateEnvironmentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The environment's name, e.g. \"production\" or \"review/my-feature\".")]
        string name,
        [Description("Optional absolute URL where this environment is reachable. Omit for none.")]
        string? externalUrl = null,
        [Description(
            "Optional deployment tier: \"production\", \"staging\", \"testing\", \"development\", or \"other\". Omit to let GitLab infer it from the name.")]
        string? tier = null,
        [Description("Optional free-text description. Omit for none.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTier(tier);

        var request = new CreateEnvironmentRequest
        {
            Name = name,
            ExternalUrl = ParseAbsoluteUri(externalUrl, nameof(externalUrl)),
            Tier = string.IsNullOrEmpty(tier) ? null : tier,
            Description = description
        };

        var environment = await environments.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(EnvironmentMapper.ToSummary(environment), "projects/:id/environments (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_pages_domain
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_pages_domain", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a custom domain to a project's Pages site, with a supplied PEM certificate and key, or via Let's Encrypt auto-SSL once the domain verifies.")]
    public async Task<CallToolResult> CreatePagesDomainAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The custom domain hostname to add, e.g. \"docs.example.com\".")]
        string domain,
        [Description("Optional PEM-encoded TLS certificate for this domain. Omit if autoSslEnabled is true.")]
        string? certificate = null,
        [Description("Optional PEM-encoded private key for the certificate. Omit if autoSslEnabled is true.")]
        string? key = null,
        [Description(
            "If true, GitLab obtains and renews a Let's Encrypt certificate for this domain instead of using certificate/key. Default false.")]
        bool? autoSslEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreatePagesDomainRequest
        {
            Domain = domain,
            Certificate = certificate,
            Key = key,
            AutoSslEnabled = autoSslEnabled
        };

        var created = await pages.CreateDomainAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToDetail(created), "projects/:id/pages/domains (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_instance_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_instance_deploy_key", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a deploy key directly on the instance, unattached to any project until a project enables it. Requires administrator access.")]
    public async Task<CallToolResult> CreateInstanceDeployKeyAsync(
        [Description("A label for the key.")] string title,
        [Description("The public key content, e.g. an \"ssh-ed25519 AAAA...\" line.")]
        string key,
        [Description(
            "If true, this key is allowed to push to repositories it is enabled on, not just pull. Default false.")]
        bool? canPush = null,
        [Description("Optional expiry, ISO 8601 (e.g. \"2027-01-01T00:00:00Z\"). Omit for no expiry.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDeployKeyRequest
        {
            Key = key,
            Title = title,
            CanPush = canPush,
            ExpiresAt = ParseTimestamp(expiresAt, nameof(expiresAt))
        };

        var created = await deployKeys.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(DeployKeyMapper.ToSummary(created), "deploy_keys (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runners
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runners", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists runners: every runner visible to the caller by default, or narrowed to one project's or one group's assigned runners. Use to find a runner by tag or status before assigning work or debugging CI capacity.")]
    public async Task<CallToolResult> ListRunnersAsync(
        [Description(
            "Restrict to runners assigned to this project: numeric id or \"namespace/path\". Omit unless scoping to a project. Cannot be combined with group.")]
        string? project = null,
        [Description(
            "Restrict to runners assigned to this group: numeric id or \"namespace/path\". Omit unless scoping to a group. Cannot be combined with project.")]
        string? group = null,
        [Description("Filter by runner scope: \"instance_type\", \"group_type\", or \"project_type\". Omit for all.")]
        string? type = null,
        [Description(
            "Filter by status: \"active\", \"paused\", \"online\", \"offline\", \"never_contacted\", or \"stale\". Omit for all.")]
        string? status = null,
        [Description(
            "If true, return only runners currently ignoring new jobs; if false, only ones accepting jobs. Omit for both.")]
        bool? paused = null,
        [Description(
            "Comma-separated tags; only runners carrying every listed tag are returned. Omit for no tag filter.")]
        string? tags = null,
        [Description("Maximum runners to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateRunnerType(type);
        ValidateRunnerStatus(status);

        if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(group))
            throw new McpException(
                "project and group cannot both be set - scope to at most one, or omit both to list runners visible to the caller.");

        var options = new RunnerListOptions
        {
            Type = string.IsNullOrEmpty(type) ? null : type,
            Status = string.IsNullOrEmpty(status) ? null : status,
            Paused = paused,
            TagList = ParseCommaList(tags),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        var source = !string.IsNullOrWhiteSpace(project)
            ? runners.ListProjectRunnersAsync(project!, options, cancellationToken)
            : !string.IsNullOrWhiteSpace(group)
                ? runners.ListGroupRunnersAsync(group!, options, cancellationToken)
                : runners.ListAsync(options, cancellationToken);

        List<RunnerSummary> collected = [];
        var truncated = false;
        await foreach (var runner in source)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerMapper.ToSummary(runner));
        }

        return GitLabContent.Wrap(new RunnerListResult(collected, truncated), "runners");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_runner
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_runner", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one runner's full configuration and status by id, e.g. to confirm its tags or online state before diagnosing a stuck pipeline.")]
    public async Task<CallToolResult> GetRunnerAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        CancellationToken cancellationToken = default)
    {
        var runner = await runners.GetAsync(runnerId, null, cancellationToken);
        return GitLabContent.Wrap(RunnerMapper.ToSummary(runner), "runners/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_runner
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_runner", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently removes a runner from the instance. This cannot be undone - the runner must be re-registered from scratch if it is needed again.")]
    public async Task<RunnerDeleteResult> DeleteRunnerAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        CancellationToken cancellationToken = default)
    {
        await runners.DeleteAsync(runnerId, cancellationToken);
        return new RunnerDeleteResult(runnerId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_assign_runner_to_project
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_assign_runner_to_project", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Grants an existing shared or project runner to another project so that project's pipelines can use it.")]
    public async Task<CallToolResult> AssignRunnerToProjectAsync(
        [Description("The project to grant the runner to: numeric id or \"namespace/path\".")]
        string project,
        [Description("The existing runner's numeric id.")]
        long runnerId,
        CancellationToken cancellationToken = default)
    {
        var request = new AssignRunnerRequest { RunnerId = runnerId };
        var runner = await runners.AssignToProjectAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(RunnerMapper.ToSummary(runner), "projects/:id/runners (assign)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_unassign_runner_from_project
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_unassign_runner_from_project", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Revokes a project's access to a runner it does not own. Fails with a validation error if aimed at the runner's own owner project - delete the runner instead of unassigning it from its own project.")]
    public async Task<RunnerProjectUnassignResult> UnassignRunnerFromProjectAsync(
        [Description("The project to revoke access from: numeric id or \"namespace/path\".")]
        string project,
        [Description("The runner's numeric id.")]
        long runnerId,
        CancellationToken cancellationToken = default)
    {
        await runners.UnassignFromProjectAsync(project, runnerId, cancellationToken);
        return new RunnerProjectUnassignResult(runnerId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_jobs
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_jobs", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists jobs a runner is processing or has processed, to diagnose load or find which runner picked up a specific job.")]
    public async Task<CallToolResult> ListRunnerJobsAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        [Description(
            "Filter by job status: \"created\", \"pending\", \"running\", \"failed\", \"success\", \"canceled\", \"skipped\", \"waiting_for_resource\", or \"manual\". Omit for all.")]
        string? status = null,
        [Description("Maximum jobs to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateJobStatus(status);

        var options = new RunnerJobListOptions
        {
            Status = string.IsNullOrEmpty(status) ? null : status,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<RunnerJobSummary> collected = [];
        var truncated = false;
        await foreach (var job in runners.ListJobsAsync(runnerId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerMapper.ToJobSummary(job));
        }

        return GitLabContent.Wrap(new RunnerJobListResult(collected, truncated), "runners/:id/jobs");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_managers
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_managers", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the machines (managers) registered behind one runner registration, each with its own version and last-contact time.")]
    public async Task<CallToolResult> ListRunnerManagersAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        [Description("Maximum managers to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<RunnerManagerSummary> collected = [];
        var truncated = false;
        await foreach (var manager in runners.ListManagersAsync(runnerId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerMapper.ToManagerSummary(manager));
        }

        return GitLabContent.Wrap(new RunnerManagerListResult(collected, truncated), "runners/:id/managers");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_projects
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_projects", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the projects a project-scoped runner is assigned to, to audit or prune its reach.")]
    public async Task<CallToolResult> ListRunnerProjectsAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        [Description("Maximum projects to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<RunnerProjectSummary> collected = [];
        var truncated = false;
        await foreach (var project in runners.ListProjectsAsync(runnerId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerMapper.ToProjectSummary(project));
        }

        return GitLabContent.Wrap(new RunnerProjectListResult(collected, truncated), "runners/:id/projects");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_reset_runner_registration_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_reset_runner_registration_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Rotates the runner registration token used to register new runners against the whole instance, one project, or one group - use when a token may have leaked. The new token's value is never returned by this server, only its expiry; resetting the instance-wide token (both project and group omitted) requires an administrator-capable token.")]
    public async Task<RunnerTokenResetResult> ResetRunnerRegistrationTokenAsync(
        [Description(
            "Reset this project's registration token instead of the instance-wide one: numeric id or \"namespace/path\". Cannot be combined with group.")]
        string? project = null,
        [Description(
            "Reset this group's registration token instead of the instance-wide one: numeric id or \"namespace/path\". Cannot be combined with project.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(group))
            throw new McpException(
                "project and group cannot both be set - scope to at most one, or omit both to reset the instance-wide token.");

        var token = !string.IsNullOrWhiteSpace(project)
            ? await runners.ResetProjectRegistrationTokenAsync(project!, cancellationToken)
            : !string.IsNullOrWhiteSpace(group)
                ? await runners.ResetGroupRegistrationTokenAsync(group!, cancellationToken)
                : await runners.ResetRegistrationTokenAsync(cancellationToken);

        return RunnerMapper.ToTokenResetResult(token);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_reset_runner_authentication_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_reset_runner_authentication_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Forces every manager currently connected under a runner to re-authenticate, by issuing it a fresh authentication token. The new token's value is never returned by this server, only its expiry.")]
    public async Task<RunnerTokenResetResult> ResetRunnerAuthenticationTokenAsync(
        [Description("The runner's numeric id.")]
        long runnerId,
        CancellationToken cancellationToken = default)
    {
        var token = await runners.ResetAuthenticationTokenAsync(runnerId, cancellationToken);
        return RunnerMapper.ToTokenResetResult(token);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_controllers
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_controllers", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the runner-controller fleet-management agents registered on the instance. Requires administrator access.")]
    public async Task<CallToolResult> ListRunnerControllersAsync(
        [Description("Maximum controllers to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<RunnerControllerSummary> collected = [];
        var truncated = false;
        await foreach (var controller in runnerControllers.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerControllerMapper.ToSummary(controller));
        }

        return GitLabContent.Wrap(new RunnerControllerListResult(collected, truncated), "runner_controllers");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_runner_controller
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_runner_controller", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one runner controller's details, including whether it is currently connected (a flag a list does not report). Requires administrator access.")]
    public async Task<CallToolResult> GetRunnerControllerAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        CancellationToken cancellationToken = default)
    {
        var controller = await runnerControllers.GetAsync(runnerControllerId, cancellationToken);
        return GitLabContent.Wrap(RunnerControllerMapper.ToSummary(controller), "runner_controllers/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_register_runner_controller
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_register_runner_controller", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Registers a new runner controller. It starts with no scope and no usable token - set up both afterward with gitlab_assign_runner_controller_scope and gitlab_create_runner_controller_token. Requires administrator access.")]
    public async Task<CallToolResult> RegisterRunnerControllerAsync(
        [Description("A label for the controller. Omit for none.")]
        string? description = null,
        [Description(
            "Initial state: \"enabled\", \"disabled\", or \"dry_run\". Omit to let GitLab choose the default.")]
        string? state = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RegisterRunnerControllerRequest
        {
            Description = description,
            State = ParseControllerState(state)
        };

        var controller = await runnerControllers.RegisterAsync(request, cancellationToken);
        return GitLabContent.Wrap(RunnerControllerMapper.ToSummary(controller), "runner_controllers (register)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_runner_controller
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_runner_controller", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Renames a runner controller or changes its enabled/disabled/dry-run state. Only the fields supplied are changed. Requires administrator access.")]
    public async Task<CallToolResult> UpdateRunnerControllerAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("New description, or omit to leave unchanged.")]
        string? description = null,
        [Description("New state: \"enabled\", \"disabled\", or \"dry_run\". Omit to leave unchanged.")]
        string? state = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateRunnerControllerRequest
        {
            Description = description,
            State = ParseControllerState(state)
        };

        var controller = await runnerControllers.UpdateAsync(runnerControllerId, request, cancellationToken);
        return GitLabContent.Wrap(RunnerControllerMapper.ToSummary(controller), "runner_controllers/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_runner_controller
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_runner_controller", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a runner controller. Requires administrator access.")]
    public async Task<RunnerControllerDeleteResult> DeleteRunnerControllerAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        CancellationToken cancellationToken = default)
    {
        await runnerControllers.DeleteAsync(runnerControllerId, cancellationToken);
        return new RunnerControllerDeleteResult(runnerControllerId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_assign_runner_controller_scope
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_assign_runner_controller_scope", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Puts a runner controller in charge of the whole instance, or - given a runner id - of one specific runner. Fails with a conflict if the controller already holds the other kind of scope. Requires administrator access.")]
    public async Task<RunnerControllerScopeSummary> AssignRunnerControllerScopeAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description(
            "Scope the controller to this one runner instead of the whole instance. Omit to scope it to the whole instance.")]
        long? runnerId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = runnerId is long id
            ? await runnerControllers.AddRunnerScopeAsync(runnerControllerId, id, cancellationToken)
            : await runnerControllers.AddInstanceScopeAsync(runnerControllerId, cancellationToken);

        return RunnerControllerMapper.ToScopeSummary(scope);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_remove_runner_controller_scope
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_remove_runner_controller_scope", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Takes a runner controller out of charge of the whole instance, or - given a runner id - of one specific runner. Requires administrator access.")]
    public async Task<RunnerControllerScopeRemoveResult> RemoveRunnerControllerScopeAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description(
            "Remove the controller's scope over this one runner instead of the whole instance. Omit to remove its instance-wide scope.")]
        long? runnerId = null,
        CancellationToken cancellationToken = default)
    {
        if (runnerId is long id)
            await runnerControllers.RemoveRunnerScopeAsync(runnerControllerId, id, cancellationToken);
        else
            await runnerControllers.RemoveInstanceScopeAsync(runnerControllerId, cancellationToken);

        return new RunnerControllerScopeRemoveResult(runnerControllerId, runnerId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_runner_controller_tokens
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_runner_controller_tokens", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a runner controller's tokens by metadata only, or - given a token id - looks up one token by id. Secrets are never listed or returned. Requires administrator access.")]
    public async Task<CallToolResult> ListRunnerControllerTokensAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("Look up this one token instead of listing every token. Omit to list all.")]
        long? tokenId = null,
        [Description("Maximum tokens to return (1-100). Ignored when tokenId is set. Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (tokenId is long id)
        {
            var single = await runnerControllers.GetTokenAsync(runnerControllerId, id, cancellationToken);
            return GitLabContent.Wrap(
                new RunnerControllerTokenListResult([RunnerControllerMapper.ToTokenSummary(single)], false),
                "runner_controllers/:id/tokens/:id");
        }

        ValidateLimit(limit);

        List<RunnerControllerTokenSummary> collected = [];
        var truncated = false;
        await foreach (var token in runnerControllers.ListTokensAsync(runnerControllerId, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(RunnerControllerMapper.ToTokenSummary(token));
        }

        return GitLabContent.Wrap(new RunnerControllerTokenListResult(collected, truncated),
            "runner_controllers/:id/tokens");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_rotate_runner_controller_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_rotate_runner_controller_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Mints a fresh secret for a runner controller token, immediately invalidating the old one. GitLab returns the new secret exactly once, but this server never returns it - only the token's metadata; retrieve the raw secret value directly from the GitLab UI or API if it is genuinely needed. Requires administrator access.")]
    public async Task<CallToolResult> RotateRunnerControllerTokenAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("The token's numeric id.")]
        long tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await runnerControllers.RotateTokenAsync(runnerControllerId, tokenId, cancellationToken);
        return GitLabContent.Wrap(RunnerControllerMapper.ToTokenSummary(token),
            "runner_controllers/:id/tokens/:id (rotate)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_revoke_runner_controller_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_revoke_runner_controller_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Immediately revokes a runner controller token so any controller still using it stops authenticating. Requires administrator access.")]
    public async Task<RunnerControllerTokenRevokeResult> RevokeRunnerControllerTokenAsync(
        [Description("The runner controller's numeric id.")]
        long runnerControllerId,
        [Description("The token's numeric id.")]
        long tokenId,
        CancellationToken cancellationToken = default)
    {
        await runnerControllers.RevokeTokenAsync(runnerControllerId, tokenId, cancellationToken);
        return new RunnerControllerTokenRevokeResult(runnerControllerId, tokenId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_environments
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_environments", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's deployment environments, optionally filtered by exact name, a name substring, or state, to see what's deployed where.")]
    public async Task<CallToolResult> ListEnvironmentsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Return only the environment with this exact name. Omit to not filter by exact name.")]
        string? name = null,
        [Description("Return only environments whose name contains this text. Omit to not filter by substring.")]
        string? search = null,
        [Description("Filter by state: \"available\", \"stopping\", or \"stopped\". Omit for all.")]
        string? state = null,
        [Description("Maximum environments to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateEnvironmentState(state);

        var options = new EnvironmentListOptions
        {
            Name = string.IsNullOrEmpty(name) ? null : name,
            Search = string.IsNullOrEmpty(search) ? null : search,
            States = string.IsNullOrEmpty(state) ? null : state,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<EnvironmentSummary> collected = [];
        var truncated = false;
        await foreach (var environment in environments.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(EnvironmentMapper.ToSummary(environment));
        }

        return GitLabContent.Wrap(new EnvironmentListResult(collected, truncated), "projects/:id/environments");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_environment", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one environment's external URL, tier, current state and description by id.")]
    public async Task<CallToolResult> GetEnvironmentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The environment's numeric id.")]
        long environmentId,
        CancellationToken cancellationToken = default)
    {
        var environment = await environments.GetAsync(project, environmentId, cancellationToken);
        return GitLabContent.Wrap(EnvironmentMapper.ToSummary(environment), "projects/:id/environments/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_environment", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes an environment's external URL, tier, Kubernetes namespace, Flux resource path, cluster agent, description, or auto-stop setting. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateEnvironmentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The environment's numeric id.")]
        long environmentId,
        [Description("New external URL, or omit to leave unchanged.")]
        string? externalUrl = null,
        [Description(
            "New deployment tier: \"production\", \"staging\", \"testing\", \"development\", or \"other\". Omit to leave unchanged.")]
        string? tier = null,
        [Description("New Kubernetes namespace, or omit to leave unchanged.")]
        string? kubernetesNamespace = null,
        [Description("New Flux resource path, or omit to leave unchanged.")]
        string? fluxResourcePath = null,
        [Description("Numeric id of a cluster agent to associate with this environment, or omit to leave unchanged.")]
        long? clusterAgentId = null,
        [Description("New free-text description, or omit to leave unchanged.")]
        string? description = null,
        [Description("New auto-stop setting, or omit to leave unchanged.")]
        string? autoStopSetting = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTier(tier);

        var request = new UpdateEnvironmentRequest
        {
            ExternalUrl = ParseAbsoluteUri(externalUrl, nameof(externalUrl)),
            Tier = string.IsNullOrEmpty(tier) ? null : tier,
            ClusterAgentId = clusterAgentId,
            KubernetesNamespace = kubernetesNamespace,
            FluxResourcePath = fluxResourcePath,
            Description = description,
            AutoStopSetting = autoStopSetting
        };

        var environment = await environments.UpdateAsync(project, environmentId, request, cancellationToken);
        return GitLabContent.Wrap(EnvironmentMapper.ToSummary(environment), "projects/:id/environments/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_stop_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_stop_environment", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Stops an environment, running its on_stop CI job if one is defined. Set force to stop it even when it has no on_stop action.")]
    public async Task<CallToolResult> StopEnvironmentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The environment's numeric id.")]
        long environmentId,
        [Description("If true, stops the environment even when it has no on_stop action defined. Default false.")]
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var environment = await environments.StopAsync(project, environmentId, force, cancellationToken);
        return GitLabContent.Wrap(EnvironmentMapper.ToSummary(environment), "projects/:id/environments/:id/stop");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_environment", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently deletes an environment record. This does not stop it first - stop it before deleting if it is still running.")]
    public async Task<EnvironmentDeleteResult> DeleteEnvironmentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The environment's numeric id.")]
        long environmentId,
        CancellationToken cancellationToken = default)
    {
        await environments.DeleteAsync(project, environmentId, cancellationToken);
        return new EnvironmentDeleteResult(environmentId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_stop_stale_environments
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_stop_stale_environments", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Bulk-stops every environment on a project last modified or deployed to before a cutoff date, skipping protected environments.")]
    public async Task<StopStaleEnvironmentsResult> StopStaleEnvironmentsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "Stop every environment last modified or deployed to before this ISO 8601 timestamp, e.g. \"2026-01-01T00:00:00Z\".")]
        string before,
        CancellationToken cancellationToken = default)
    {
        var cutoff = ParseTimestamp(before, nameof(before))
                     ?? throw new McpException("before must be an ISO 8601 timestamp (e.g. \"2026-01-01T00:00:00Z\").");

        await environments.StopStaleAsync(project, new StopStaleEnvironmentsRequest { Before = cutoff },
            cancellationToken);
        return new StopStaleEnvironmentsResult(cutoff, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_stale_review_apps
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_stale_review_apps", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Schedules stopped review-app environments on a project for deletion a week out. GitLab defaults to a dry run - nothing scheduled - unless dryRun is explicitly set to false. This call never returns the preview/scheduled list; GitLab computes one but this method discards it.")]
    public async Task<ReviewAppDeletionResult> DeleteStaleReviewAppsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "Only consider review apps stopped before this ISO 8601 timestamp. Omit for GitLab's default (30 days ago).")]
        string? before = null,
        [Description(
            "Maximum number of review apps to schedule for deletion (1-100). Omit for GitLab's default (100).")]
        int? limit = null,
        [Description(
            "If false, actually schedules deletion. Omit or true to preview-only - GitLab's own default is a dry run.")]
        bool? dryRun = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptionalLimit(limit);

        var cutoff = ParseTimestamp(before, nameof(before));

        var options = new ReviewAppDeletionOptions
        {
            Before = cutoff,
            Limit = limit,
            DryRun = dryRun
        };

        await environments.DeleteReviewAppsAsync(project, options, cancellationToken);
        return new ReviewAppDeletionResult(dryRun ?? true, cutoff, limit, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_deployments
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_deployments", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's deployments, filterable by environment, status, or an updated-at date range, to see recent shipping activity.")]
    public async Task<CallToolResult> ListDeploymentsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Restrict to deployments to this environment name. Omit for all.")]
        string? environment = null,
        [Description(
            "Filter by status: \"created\", \"running\", \"success\", \"failed\", \"canceled\", \"skipped\", or \"blocked\". Omit for all.")]
        string? status = null,
        [Description("Only include deployments updated at or after this ISO 8601 timestamp. Omit for no lower bound.")]
        string? updatedAfter = null,
        [Description("Only include deployments updated at or before this ISO 8601 timestamp. Omit for no upper bound.")]
        string? updatedBefore = null,
        [Description(
            "Sort field: \"id\", \"iid\", \"created_at\", \"updated_at\", or \"finished_at\". Omit for GitLab's default (\"id\").")]
        string? orderBy = null,
        [Description("Sort order: \"asc\" or \"desc\". Omit for GitLab's default.")]
        string? sort = null,
        [Description("Maximum deployments to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateDeploymentFilterStatus(status);
        ValidateDeploymentOrderBy(orderBy);
        ValidateSort(sort);

        var options = new DeploymentListOptions
        {
            Environment = string.IsNullOrEmpty(environment) ? null : environment,
            Status = string.IsNullOrEmpty(status) ? null : status,
            UpdatedAfter = ParseTimestamp(updatedAfter, nameof(updatedAfter)),
            UpdatedBefore = ParseTimestamp(updatedBefore, nameof(updatedBefore)),
            OrderBy = string.IsNullOrEmpty(orderBy) ? null : orderBy,
            Sort = string.IsNullOrEmpty(sort) ? null : sort,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<DeploymentSummary> collected = [];
        var truncated = false;
        await foreach (var deployment in deployments.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeploymentMapper.ToSummary(deployment));
        }

        return GitLabContent.Wrap(new DeploymentListResult(collected, truncated), "projects/:id/deployments");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_deployment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_deployment", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one deployment's full details, including its triggering CI job and any pending or most recent approval decision.")]
    public async Task<CallToolResult> GetDeploymentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deployment's numeric id.")]
        long deploymentId,
        CancellationToken cancellationToken = default)
    {
        var deployment = await deployments.GetAsync(project, deploymentId, cancellationToken);
        return GitLabContent.Wrap(DeploymentMapper.ToDetail(deployment), "projects/:id/deployments/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_deployment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_deployment", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Records a deployment performed outside GitLab CI/CD against an environment - e.g. from an external deployment tool. GitLab creates the environment if it does not already exist.")]
    public async Task<CallToolResult> CreateDeploymentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The name of the environment being deployed to.")]
        string environment,
        [Description("The commit SHA that was deployed.")]
        string sha,
        [Description("The branch or tag name that was deployed.")]
        string refName,
        [Description("The status to record: \"running\", \"success\", \"failed\", or \"canceled\".")]
        string status,
        [Description("True if refName is a tag rather than a branch. Default false.")]
        bool tag = false,
        CancellationToken cancellationToken = default)
    {
        ValidateDeploymentWriteStatus(status);

        var request = new CreateDeploymentRequest
        {
            Environment = environment,
            Sha = sha,
            Ref = refName,
            Tag = tag,
            Status = status
        };

        var deployment = await deployments.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(DeploymentMapper.ToSummary(deployment), "projects/:id/deployments (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_deployment_status
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_deployment_status", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Moves a deployment to a new status such as success, failed, or canceled.")]
    public async Task<CallToolResult> UpdateDeploymentStatusAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deployment's numeric id.")]
        long deploymentId,
        [Description("The new status: \"running\", \"success\", \"failed\", or \"canceled\".")]
        string status,
        CancellationToken cancellationToken = default)
    {
        ValidateDeploymentWriteStatus(status);

        var deployment = await deployments.UpdateAsync(project, deploymentId,
            new UpdateDeploymentRequest { Status = status }, cancellationToken);
        return GitLabContent.Wrap(DeploymentMapper.ToSummary(deployment), "projects/:id/deployments/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_deployment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_deployment", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a finished deployment that is not the current one for its environment. GitLab refuses to delete the current deployment for an environment.")]
    public async Task<DeploymentDeleteResult> DeleteDeploymentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deployment's numeric id.")]
        long deploymentId,
        CancellationToken cancellationToken = default)
    {
        await deployments.DeleteAsync(project, deploymentId, cancellationToken);
        return new DeploymentDeleteResult(deploymentId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_approve_deployment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_approve_deployment", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Approves or rejects a deployment that is blocked on a protected environment's approval rules.")]
    public async Task<CallToolResult> ApproveDeploymentAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deployment's numeric id.")]
        long deploymentId,
        [Description("The decision: \"approved\" or \"rejected\".")]
        string status,
        [Description("Optional comment recorded alongside the decision. Omit for none.")]
        string? comment = null,
        [Description(
            "The name of the user, group, or role to approve as. Only needed - and required by GitLab - when the caller matches more than one of the protected environment's approval rules.")]
        string? representedAs = null,
        CancellationToken cancellationToken = default)
    {
        ValidateApprovalStatus(status);

        var request = new ApproveDeploymentRequest
        {
            Status = status,
            Comment = comment,
            RepresentedAs = representedAs
        };

        var approval = await deployments.ApproveAsync(project, deploymentId, request, cancellationToken);
        return GitLabContent.Wrap(DeploymentMapper.ToApprovalSummary(approval),
            "projects/:id/deployments/:id/approval");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_protected_environments
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_protected_environments", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the protected environments configured on a project, or the protected deployment tiers configured on a group - who may deploy to each, and who must approve.")]
    public async Task<CallToolResult> ListProtectedEnvironmentsAsync(
        [Description(
            "Project to list protected environments for: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group to list protected deployment tiers for: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        [Description("Maximum entries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateProjectXorGroup(project, group);

        var source = !string.IsNullOrWhiteSpace(project)
            ? protectedEnvironments.ListForProjectAsync(project!, cancellationToken)
            : protectedEnvironments.ListForGroupAsync(group!, cancellationToken);

        List<ProtectedEnvironmentSummary> collected = [];
        var truncated = false;
        await foreach (var protectedEnvironment in source)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ProtectedEnvironmentMapper.ToSummary(protectedEnvironment));
        }

        return GitLabContent.Wrap(new ProtectedEnvironmentListResult(collected, truncated), "protected_environments");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_protected_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_protected_environment", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one protected environment's (project scope) or protected deployment tier's (group scope) deploy-access entries and approval rules, by name.")]
    public async Task<CallToolResult> GetProtectedEnvironmentAsync(
        [Description(
            "The environment name (project scope) or deployment tier (group scope) to look up, e.g. \"production\".")]
        string name,
        [Description(
            "Project the environment belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group the deployment tier belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        var protectedEnvironment = !string.IsNullOrWhiteSpace(project)
            ? await protectedEnvironments.GetForProjectAsync(project!, name, cancellationToken)
            : await protectedEnvironments.GetForGroupAsync(group!, name, cancellationToken);

        return GitLabContent.Wrap(ProtectedEnvironmentMapper.ToSummary(protectedEnvironment),
            "protected_environments/:name");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_protect_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_protect_environment", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Protects an environment (project scope) or a deployment tier (group scope) so only a chosen user, group or role may deploy to it, optionally requiring approval first. Fails if the name is already protected - unprotect it first to change who may deploy.")]
    public async Task<CallToolResult> ProtectEnvironmentAsync(
        [Description(
            "The environment name to protect on a project (e.g. \"production\", \"review/*\"), or the deployment tier to protect on a group: \"production\", \"staging\", \"testing\", \"development\", or \"other\".")]
        string name,
        [Description(
            "Project to protect an environment on: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group to protect a deployment tier on: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        [Description(
            "GitLab access level allowed to deploy: 20=Reporter, 30=Developer, 40=Maintainer, 60=Admin. Exactly one of deployAccessLevel, deployUserId, or deployGroupId must be set.")]
        int? deployAccessLevel = null,
        [Description(
            "Numeric id of the one user allowed to deploy. Exactly one of deployAccessLevel, deployUserId, or deployGroupId must be set.")]
        long? deployUserId = null,
        [Description(
            "Numeric id of the one group allowed to deploy. Exactly one of deployAccessLevel, deployUserId, or deployGroupId must be set.")]
        long? deployGroupId = null,
        [Description(
            "Number of approvals required before a deployment can proceed. Omit for none (this is GitLab's legacy environment-wide approval count).")]
        int? requiredApprovalCount = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        var accessEntrySetCount = (deployAccessLevel is not null ? 1 : 0)
                                  + (deployUserId is not null ? 1 : 0)
                                  + (deployGroupId is not null ? 1 : 0);
        if (accessEntrySetCount != 1)
            throw new McpException("exactly one of deployAccessLevel, deployUserId, or deployGroupId must be set.");

        var request = new ProtectEnvironmentRequest
        {
            Name = name,
            DeployAccessLevels =
            [
                new DeployAccessLevelRequest
                {
                    AccessLevel = deployAccessLevel,
                    UserId = deployUserId,
                    GroupId = deployGroupId
                }
            ],
            RequiredApprovalCount = requiredApprovalCount
        };

        var protectedEnvironment = !string.IsNullOrWhiteSpace(project)
            ? await protectedEnvironments.ProtectForProjectAsync(project!, request, cancellationToken)
            : await protectedEnvironments.ProtectForGroupAsync(group!, request, cancellationToken);

        return GitLabContent.Wrap(ProtectedEnvironmentMapper.ToSummary(protectedEnvironment),
            "protected_environments (protect)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_protected_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_protected_environment", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Changes an already-protected environment's (project scope) or protected deployment tier's (group scope) required approval count, and/or adds or removes one deploy-access entry. Fields left unset are unchanged. Approval rules cannot be edited by this tool - unprotect and re-protect if they need to change.")]
    public async Task<CallToolResult> UpdateProtectedEnvironmentAsync(
        [Description(
            "The environment name (project scope) or deployment tier (group scope) to update, e.g. \"production\".")]
        string name,
        [Description(
            "Project the environment belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group the deployment tier belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        [Description("New required-approval count, or omit to leave unchanged.")]
        int? requiredApprovalCount = null,
        [Description(
            "GitLab access level for a new deploy-access entry to add: 20=Reporter, 30=Developer, 40=Maintainer, 60=Admin. At most one of addDeployAccessLevel, addDeployUserId, or addDeployGroupId may be set.")]
        int? addDeployAccessLevel = null,
        [Description(
            "Numeric id of a user to add as a new deploy-access entry. At most one of addDeployAccessLevel, addDeployUserId, or addDeployGroupId may be set.")]
        long? addDeployUserId = null,
        [Description(
            "Numeric id of a group to add as a new deploy-access entry. At most one of addDeployAccessLevel, addDeployUserId, or addDeployGroupId may be set.")]
        long? addDeployGroupId = null,
        [Description(
            "Numeric id of an existing deploy-access entry (from gitlab_get_protected_environment) to remove. Omit to remove none.")]
        long? removeDeployAccessLevelId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        var addCount = (addDeployAccessLevel is not null ? 1 : 0) + (addDeployUserId is not null ? 1 : 0) +
                       (addDeployGroupId is not null ? 1 : 0);
        if (addCount > 1)
            throw new McpException(
                "at most one of addDeployAccessLevel, addDeployUserId, or addDeployGroupId may be set.");

        List<DeployAccessLevelRequest>? entries = null;
        if (addCount == 1 || removeDeployAccessLevelId is not null)
        {
            entries = [];

            if (addCount == 1)
                entries.Add(new DeployAccessLevelRequest
                {
                    AccessLevel = addDeployAccessLevel,
                    UserId = addDeployUserId,
                    GroupId = addDeployGroupId
                });

            if (removeDeployAccessLevelId is long removeId)
                entries.Add(new DeployAccessLevelRequest { Id = removeId, Destroy = true });
        }

        var request = new UpdateProtectedEnvironmentRequest
        {
            RequiredApprovalCount = requiredApprovalCount,
            DeployAccessLevels = entries
        };

        var protectedEnvironment = !string.IsNullOrWhiteSpace(project)
            ? await protectedEnvironments.UpdateForProjectAsync(project!, name, request, cancellationToken)
            : await protectedEnvironments.UpdateForGroupAsync(group!, name, request, cancellationToken);

        return GitLabContent.Wrap(ProtectedEnvironmentMapper.ToSummary(protectedEnvironment),
            "protected_environments/:name (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_unprotect_environment
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_unprotect_environment", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes protection from an environment (project scope) or a deployment tier (group scope), leaving the environment itself in place. Does not undo any deployment already approved or blocked under the removed rule.")]
    public async Task<EnvironmentUnprotectResult> UnprotectEnvironmentAsync(
        [Description(
            "The environment name (project scope) or deployment tier (group scope) to unprotect, e.g. \"production\".")]
        string name,
        [Description(
            "Project the environment belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group the deployment tier belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        if (!string.IsNullOrWhiteSpace(project))
            await protectedEnvironments.UnprotectForProjectAsync(project!, name, cancellationToken);
        else
            await protectedEnvironments.UnprotectForGroupAsync(group!, name, cancellationToken);

        return new EnvironmentUnprotectResult(name, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_freeze_periods
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_freeze_periods", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's deployment freeze windows - date ranges during which GitLab blocks new deployments.")]
    public async Task<CallToolResult> ListFreezePeriodsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum freeze periods to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<FreezePeriodSummary> collected = [];
        var truncated = false;
        await foreach (var period in freezePeriods.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(FreezePeriodMapper.ToSummary(period));
        }

        return GitLabContent.Wrap(new FreezePeriodListResult(collected, truncated), "projects/:id/freeze_periods");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_freeze_period
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_freeze_period", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one freeze period's cron start/end boundaries and time zone by id.")]
    public async Task<CallToolResult> GetFreezePeriodAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The freeze period's numeric id.")]
        long freezePeriodId,
        CancellationToken cancellationToken = default)
    {
        var period = await freezePeriods.GetAsync(project, freezePeriodId, cancellationToken);
        return GitLabContent.Wrap(FreezePeriodMapper.ToSummary(period), "projects/:id/freeze_periods/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_freeze_period
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_freeze_period", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Defines a new deploy freeze window with cron start/end expressions, e.g. to block releases over a holiday.")]
    public async Task<CallToolResult> CreateFreezePeriodAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Cron expression for when the freeze starts, e.g. \"0 0 24 12 *\".")]
        string freezeStart,
        [Description("Cron expression for when the freeze ends, e.g. \"0 0 2 1 *\".")]
        string freezeEnd,
        [Description(
            "IANA time zone the cron expressions are evaluated in, e.g. \"America/New_York\". Omit for GitLab's default (UTC).")]
        string? cronTimezone = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateFreezePeriodRequest
        {
            FreezeStart = freezeStart,
            FreezeEnd = freezeEnd,
            CronTimezone = cronTimezone
        };

        var period = await freezePeriods.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(FreezePeriodMapper.ToSummary(period), "projects/:id/freeze_periods (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_freeze_period
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_freeze_period", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a freeze period's cron start/end boundaries or time zone. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateFreezePeriodAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The freeze period's numeric id.")]
        long freezePeriodId,
        [Description("New cron expression for when the freeze starts. Omit to leave unchanged.")]
        string? freezeStart = null,
        [Description("New cron expression for when the freeze ends. Omit to leave unchanged.")]
        string? freezeEnd = null,
        [Description("New IANA time zone the cron expressions are evaluated in. Omit to leave unchanged.")]
        string? cronTimezone = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateFreezePeriodRequest
        {
            FreezeStart = freezeStart,
            FreezeEnd = freezeEnd,
            CronTimezone = cronTimezone
        };

        var period = await freezePeriods.UpdateAsync(project, freezePeriodId, request, cancellationToken);
        return GitLabContent.Wrap(FreezePeriodMapper.ToSummary(period), "projects/:id/freeze_periods/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_freeze_period
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_freeze_period", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Removes a freeze period, immediately lifting the freeze it described.")]
    public async Task<FreezePeriodDeleteResult> DeleteFreezePeriodAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The freeze period's numeric id.")]
        long freezePeriodId,
        CancellationToken cancellationToken = default)
    {
        await freezePeriods.DeleteAsync(project, freezePeriodId, cancellationToken);
        return new FreezePeriodDeleteResult(freezePeriodId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_releases
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_releases", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's releases, newest released-at first. For releases across every project in a group instead, use gitlab_list_group_releases.")]
    public async Task<CallToolResult> ListReleasesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum releases to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<ReleaseSummary> collected = [];
        var truncated = false;
        await foreach (var release in releases.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ReleaseMapper.ToSummary(release));
        }

        return GitLabContent.Wrap(new ReleaseListResult(collected, truncated), "projects/:id/releases");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_release
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_release", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one release's notes, milestones and metadata by its Git tag.")]
    public async Task<CallToolResult> GetReleaseAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        var release = await releases.GetAsync(project, tagName, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToSummary(release), "projects/:id/releases/:tag_name");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_release
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_release", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Edits an existing release's name, notes, release date or linked milestones. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateReleaseAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("New release title, or omit to leave unchanged.")]
        string? name = null,
        [Description("New release notes in GitLab-flavored Markdown, or omit to leave unchanged.")]
        string? description = null,
        [Description("New release date/time, ISO 8601 (e.g. \"2026-09-09T00:00:00Z\"). Omit to leave unchanged.")]
        string? releasedAt = null,
        [Description(
            "Comma-separated milestone titles to link, replacing the current set. Omit to leave unchanged. Cannot be combined with milestoneIds.")]
        string? milestones = null,
        [Description(
            "Comma-separated numeric milestone ids to link, replacing the current set. Omit to leave unchanged. Cannot be combined with milestones.")]
        string? milestoneIds = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(milestones) && !string.IsNullOrWhiteSpace(milestoneIds))
            throw new McpException("milestones and milestoneIds cannot both be set - use at most one.");

        var request = new UpdateReleaseRequest
        {
            Name = name,
            Description = description,
            ReleasedAt = ParseTimestamp(releasedAt, nameof(releasedAt)),
            Milestones = ParseCommaList(milestones),
            MilestoneIds = ParseCommaLongList(milestoneIds, nameof(milestoneIds))
        };

        var release = await releases.UpdateAsync(project, tagName, request, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToSummary(release), "projects/:id/releases/:tag_name (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_release
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_release", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a release, leaving its underlying Git tag in place.")]
    public async Task<ReleaseDeleteResult> DeleteReleaseAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        await releases.DeleteAsync(project, tagName, cancellationToken);
        return new ReleaseDeleteResult(tagName, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_generate_release_evidence
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_generate_release_evidence", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Collects a fresh compliance evidence snapshot for an existing release and returns the release's full, updated evidence list.")]
    public async Task<CallToolResult> GenerateReleaseEvidenceAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        CancellationToken cancellationToken = default)
    {
        var release = await releases.GenerateEvidenceAsync(project, tagName, cancellationToken);
        var evidences = release.Evidences?.Select(ReleaseMapper.ToEvidenceSummary).ToList() ?? [];
        return GitLabContent.Wrap(new GenerateReleaseEvidenceResult(release.TagName, evidences),
            "projects/:id/releases/:tag_name/evidence (generate)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_release_links
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_release_links", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the downloadable asset links attached to a release.")]
    public async Task<CallToolResult> ListReleaseLinksAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("Maximum links to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new ReleaseLinkListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<ReleaseLinkSummary> collected = [];
        var truncated = false;
        await foreach (var link in releases.ListLinksAsync(project, tagName, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(ReleaseMapper.ToLinkSummary(link));
        }

        return GitLabContent.Wrap(new ReleaseLinkListResult(collected, truncated),
            "projects/:id/releases/:tag_name/assets/links");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_release_link
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_release_link", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one release asset link by id.")]
    public async Task<CallToolResult> GetReleaseLinkAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("The link's numeric id.")]
        long linkId,
        CancellationToken cancellationToken = default)
    {
        var link = await releases.GetLinkAsync(project, tagName, linkId, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToLinkSummary(link),
            "projects/:id/releases/:tag_name/assets/links/:link_id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_release_link
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_release_link", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Attaches a downloadable asset link (binary, package, image or runbook) to a release.")]
    public async Task<CallToolResult> CreateReleaseLinkAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("A label for the link.")] string name,
        [Description("The absolute URL the link points to.")]
        string url,
        [Description(
            "Optional path (relative to the project) that identifies this as a direct asset, enabling the :tag_name/downloads/:direct_asset_path shortcut. Omit for none.")]
        string? directAssetPath = null,
        [Description(
            "Link type: \"other\", \"runbook\", \"image\", or \"package\". Omit for GitLab's default (\"other\").")]
        string? linkType = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateReleaseLinkRequest
        {
            Name = name,
            Url = ParseAbsoluteUri(url, nameof(url)) ?? throw new McpException("url must be an absolute URL."),
            DirectAssetPath = directAssetPath,
            LinkType = ParseReleaseLinkType(linkType)
        };

        var link = await releases.CreateLinkAsync(project, tagName, request, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToLinkSummary(link),
            "projects/:id/releases/:tag_name/assets/links (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_release_link
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_release_link", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a release asset link's name, URL, direct asset path, or link type. Only the fields supplied are changed.")]
    public async Task<CallToolResult> UpdateReleaseLinkAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("The link's numeric id.")]
        long linkId,
        [Description("New label for the link. Omit to leave unchanged.")]
        string? name = null,
        [Description("New absolute URL. Omit to leave unchanged.")]
        string? url = null,
        [Description("New direct asset path. Omit to leave unchanged.")]
        string? directAssetPath = null,
        [Description("New link type: \"other\", \"runbook\", \"image\", or \"package\". Omit to leave unchanged.")]
        string? linkType = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateReleaseLinkRequest
        {
            Name = name,
            Url = ParseAbsoluteUri(url, nameof(url)),
            DirectAssetPath = directAssetPath,
            LinkType = ParseReleaseLinkType(linkType)
        };

        var link = await releases.UpdateLinkAsync(project, tagName, linkId, request, cancellationToken);
        return GitLabContent.Wrap(ReleaseMapper.ToLinkSummary(link),
            "projects/:id/releases/:tag_name/assets/links/:link_id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_release_link
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_release_link", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Removes an asset link from a release.")]
    public async Task<ReleaseLinkDeleteResult> DeleteReleaseLinkAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("The link's numeric id.")]
        long linkId,
        CancellationToken cancellationToken = default)
    {
        await releases.DeleteLinkAsync(project, tagName, linkId, cancellationToken);
        return new ReleaseLinkDeleteResult(linkId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_latest_release
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_latest_release", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Resolves a project's most-recently-released release without needing to know its tag name in advance, or - given suffixPath - a path relative to that release, e.g. one of its assets. Never returns raw bytes: a resolved release comes back as a compact summary, and a resolved asset comes back as file metadata only (name, content type, size) - never its content.")]
    public async Task<CallToolResult> GetLatestReleaseAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "Optional path relative to the latest release (e.g. an asset's direct_asset_path under \"/downloads/\"). Omit to resolve the release itself.")]
        string? suffixPath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(suffixPath))
        {
            // GitLabFileResponse (not its Content stream alone) is what owns disposal of the body, the
            // underlying HTTP response and the per-operation cancellation source -- see its XML doc.
            await using var file = await releases.GetLatestReleaseAsync(project, cancellationToken);
            using var reader = new StreamReader(file.Content);
            var json = await reader.ReadToEndAsync(cancellationToken);

            return GitLabContent.Wrap(
                new LatestReleaseResult(ParseLatestReleaseJson(json), null),
                "projects/:id/releases/permalink/latest");
        }

        await using var asset = await releases.GetLatestReleaseSuffixPathAsync(project, suffixPath, cancellationToken);
        var assetSummary = new LatestReleaseAssetSummary(asset.FileName, asset.ContentType, asset.ContentLength,
            (int)asset.StatusCode);

        return GitLabContent.Wrap(
            new LatestReleaseResult(null, assetSummary),
            "projects/:id/releases/permalink/latest/:suffix_path");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_download_release_asset
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_download_release_asset", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Downloads one asset file attached to a release, addressed by its direct asset path (from gitlab_list_release_links). Returns the file's raw bytes base64-encoded. Refuses files over 4 MB rather than returning a truncated, unusable partial file - fetch it directly from GitLab instead in that case.")]
    public async Task<CallToolResult> DownloadReleaseAssetAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The release's Git tag name.")]
        string tagName,
        [Description("The asset's direct asset path, e.g. from gitlab_list_release_links' DirectAssetUrl.")]
        string directAssetPath,
        CancellationToken cancellationToken = default)
    {
        // GitLabFileResponse (not its Content stream alone) is what owns disposal of the body, the
        // underlying HTTP response and the per-operation cancellation source -- see its XML doc.
        await using var file =
            await releases.DownloadReleaseAssetAsync(project, tagName, directAssetPath, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxFileBytes)
            throw new McpException(
                $"Asset is {declaredLength} bytes, over the {MaxFileBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var buffer = new byte[MaxFileBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxFileBytes)
            throw new McpException(
                $"Asset exceeds the {MaxFileBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var payload = new ReleaseAssetDownload(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/releases/:tag_name/downloads/:direct_asset_path");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_deploy_keys
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_deploy_keys", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the deploy keys attached to a project.")]
    public async Task<CallToolResult> ListDeployKeysAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum deploy keys to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<DeployKeySummary> collected = [];
        var truncated = false;
        await foreach (var key in deployKeys.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeployKeyMapper.ToSummary(key));
        }

        return GitLabContent.Wrap(new DeployKeyListResult(collected, truncated), "projects/:id/deploy_keys");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_deploy_key", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one of a project's deploy keys by id, including its fingerprint and push permission.")]
    public async Task<CallToolResult> GetDeployKeyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deploy key's numeric id.")]
        long deployKeyId,
        CancellationToken cancellationToken = default)
    {
        var key = await deployKeys.GetAsync(project, deployKeyId, cancellationToken);
        return GitLabContent.Wrap(DeployKeyMapper.ToSummary(key), "projects/:id/deploy_keys/:key_id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_add_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_add_deploy_key", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Adds a brand-new deploy key (public key plus title) to a project.")]
    public async Task<CallToolResult> AddDeployKeyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("A label for the key.")] string title,
        [Description("The public key content, e.g. an \"ssh-ed25519 AAAA...\" line.")]
        string key,
        [Description(
            "If true, this key is allowed to push to the project's repository, not just pull. GitLab defaults this to false.")]
        bool? canPush = null,
        [Description("Optional expiry, ISO 8601 (e.g. \"2027-01-01T00:00:00Z\"). Omit for no expiry.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateDeployKeyRequest
        {
            Key = key,
            Title = title,
            CanPush = canPush,
            ExpiresAt = ParseTimestamp(expiresAt, nameof(expiresAt))
        };

        var created = await deployKeys.AddAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(DeployKeyMapper.ToSummary(created), "projects/:id/deploy_keys (add)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_deploy_key", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Renames a deploy key or changes its push permission. Only the fields supplied are changed. GitLab's response never echoes the push permission back, even on success.")]
    public async Task<CallToolResult> UpdateDeployKeyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deploy key's numeric id.")]
        long deployKeyId,
        [Description("New title, or omit to leave unchanged.")]
        string? title = null,
        [Description("If true, allow the key to push; if false, restrict it to pull only. Omit to leave unchanged.")]
        bool? canPush = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateDeployKeyRequest { Title = title, CanPush = canPush };
        var updated = await deployKeys.UpdateAsync(project, deployKeyId, request, cancellationToken);
        return GitLabContent.Wrap(DeployKeyMapper.ToSummary(updated), "projects/:id/deploy_keys/:key_id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_deploy_key", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Removes a deploy key from a project. If no other project uses it, GitLab deletes it entirely.")]
    public async Task<DeployKeyDeleteResult> DeleteDeployKeyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The deploy key's numeric id.")]
        long deployKeyId,
        CancellationToken cancellationToken = default)
    {
        await deployKeys.DeleteAsync(project, deployKeyId, cancellationToken);
        return new DeployKeyDeleteResult(deployKeyId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_enable_deploy_key
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_enable_deploy_key", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Grants this project access to a deploy key that already exists on another project the caller can administer. Does not create a new key - use gitlab_add_deploy_key for that.")]
    public async Task<CallToolResult> EnableDeployKeyAsync(
        [Description("Project to grant access to: numeric id or \"namespace/path\".")]
        string project,
        [Description("The existing deploy key's numeric id, from a project that already has it.")]
        long deployKeyId,
        CancellationToken cancellationToken = default)
    {
        var key = await deployKeys.EnableAsync(project, deployKeyId, cancellationToken);
        return GitLabContent.Wrap(DeployKeyMapper.ToSummary(key), "projects/:id/deploy_keys/:key_id/enable");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_deploy_tokens
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_deploy_tokens", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the deploy tokens on a project or a group. Never returns a usable token value - only metadata. Exactly one of project or group must be set.")]
    public async Task<CallToolResult> ListDeployTokensAsync(
        [Description(
            "Project to list deploy tokens on: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group to list deploy tokens on: numeric id or \"namespace/path\". A group deploy token works against every project in the group. Exactly one of project or group must be set.")]
        string? group = null,
        [Description("If true, return only active tokens; if false, only revoked or expired ones. Omit for all.")]
        bool? active = null,
        [Description("Maximum deploy tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);
        ValidateProjectXorGroup(project, group);

        var options = new DeployTokenListOptions { Active = active, PerPage = Math.Min(limit + 1, MaxLimit) };

        var source = !string.IsNullOrWhiteSpace(project)
            ? deployTokens.ListForProjectAsync(project!, options, cancellationToken)
            : deployTokens.ListForGroupAsync(group!, options, cancellationToken);

        List<DeployTokenSummary> collected = [];
        var truncated = false;
        await foreach (var token in source)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(DeployTokenMapper.ToSummary(token));
        }

        return GitLabContent.Wrap(new DeployTokenListResult(collected, truncated), "deploy_tokens");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_deploy_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_deploy_token", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one deploy token's metadata by id, on a project or a group. Never returns a usable token value - only metadata. Exactly one of project or group must be set.")]
    public async Task<CallToolResult> GetDeployTokenAsync(
        [Description("The deploy token's numeric id.")]
        long deployTokenId,
        [Description(
            "Project the token belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group the token belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        var token = !string.IsNullOrWhiteSpace(project)
            ? await deployTokens.GetForProjectAsync(project!, deployTokenId, cancellationToken)
            : await deployTokens.GetForGroupAsync(group!, deployTokenId, cancellationToken);

        return GitLabContent.Wrap(DeployTokenMapper.ToSummary(token), "deploy_tokens/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_deploy_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_deploy_token", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a deploy token on a project or a group. GitLab discloses the plaintext token value only in this one response and can never show it again afterward - persist it immediately. Exactly one of project or group must be set.")]
    public async Task<CallToolResult> CreateDeployTokenAsync(
        [Description("A name for the token, shown in the GitLab UI. Not part of the credential itself.")]
        string name,
        [Description(
            "At least one scope: \"read_repository\", \"read_registry\", \"write_registry\", \"read_package_registry\", \"write_package_registry\", \"read_virtual_registry\", \"write_virtual_registry\".")]
        string[] scopes,
        [Description(
            "Project to create the token on: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group to create the token on: numeric id or \"namespace/path\". A group deploy token works against every project in the group, including projects added later. Exactly one of project or group must be set.")]
        string? group = null,
        [Description("Username half of the credential. Omit to let GitLab assign \"gitlab+deploy-token-{n}\".")]
        string? username = null,
        [Description("Optional expiry, ISO 8601 (e.g. \"2027-01-01T00:00:00Z\"). Omit for a token that never expires.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        if (scopes.Length == 0) throw new McpException("scopes must contain at least one scope.");

        var request = new CreateDeployTokenRequest
        {
            Name = name,
            Scopes = scopes,
            Username = username,
            ExpiresAt = ParseTimestamp(expiresAt, nameof(expiresAt))
        };

        var token = !string.IsNullOrWhiteSpace(project)
            ? await deployTokens.CreateForProjectAsync(project!, request, cancellationToken)
            : await deployTokens.CreateForGroupAsync(group!, request, cancellationToken);

        return GitLabContent.Wrap(DeployTokenMapper.ToCreated(token), "deploy_tokens (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_deploy_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_deploy_token", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Revokes a deploy token immediately, on a project or a group. Exactly one of project or group must be set.")]
    public async Task<DeployTokenDeleteResult> DeleteDeployTokenAsync(
        [Description("The deploy token's numeric id.")]
        long deployTokenId,
        [Description(
            "Project the token belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? project = null,
        [Description(
            "Group the token belongs to: numeric id or \"namespace/path\". Exactly one of project or group must be set.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectXorGroup(project, group);

        if (!string.IsNullOrWhiteSpace(project))
            await deployTokens.DeleteForProjectAsync(project!, deployTokenId, cancellationToken);
        else
            await deployTokens.DeleteForGroupAsync(group!, deployTokenId, cancellationToken);

        return new DeployTokenDeleteResult(deployTokenId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_upload_secure_file
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_upload_secure_file", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Uploads a new secure file (a CI/CD signing certificate, provisioning profile, or similar) to a project under a unique name. GitLab enforces one file per project per name - a second upload under the same name is rejected rather than replacing the first. Provide the file's raw bytes base64-encoded; files whose decoded size exceeds 4 MB are refused rather than silently truncated.")]
    public async Task<CallToolResult> UploadSecureFileAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "The file name GitLab stores the upload under. A second upload under a name already used on this project is rejected.")]
        string name,
        [Description("The file's raw bytes, base64-encoded.")]
        string contentBase64,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            throw new McpException("contentBase64 is not valid base64.");
        }

        if (bytes.Length > MaxFileBytes)
            throw new McpException(
                $"Decoded file is {bytes.Length} bytes, over the {MaxFileBytes}-byte limit this tool can upload. Upload it directly to GitLab instead.");

        await using var stream = new MemoryStream(bytes);
        var upload = new GitLabFileUpload { Content = stream, FileName = name };

        var file = await secureFiles.CreateAsync(project, name, upload, cancellationToken);
        return GitLabContent.Wrap(SecureFileMapper.ToSummary(file), "projects/:id/secure_files (upload)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_secure_file
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_secure_file", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one of a project's secure files' metadata - checksum, expiry, and whatever GitLab parsed out of it - by id. Never returns the file contents.")]
    public async Task<CallToolResult> GetSecureFileAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The secure file's numeric id.")]
        long secureFileId,
        CancellationToken cancellationToken = default)
    {
        var file = await secureFiles.GetAsync(project, secureFileId, cancellationToken);
        return GitLabContent.Wrap(SecureFileMapper.ToSummary(file), "projects/:id/secure_files/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_download_secure_file
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_download_secure_file", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Downloads a secure file's raw contents, base64-encoded. Refuses files over 4 MB rather than returning a truncated, unusable partial file - fetch it directly from GitLab instead in that case. Treat the returned content as a sensitive secret (a certificate, provisioning profile or keystore) - handle and store it accordingly.")]
    public async Task<CallToolResult> DownloadSecureFileAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The secure file's numeric id.")]
        long secureFileId,
        CancellationToken cancellationToken = default)
    {
        // GitLabFileResponse (not its Content stream alone) is what owns disposal of the body, the
        // underlying HTTP response and the per-operation cancellation source -- see its XML doc.
        await using var file = await secureFiles.DownloadAsync(project, secureFileId, cancellationToken);

        if (file.ContentLength is { } declaredLength && declaredLength > MaxFileBytes)
            throw new McpException(
                $"Secure file is {declaredLength} bytes, over the {MaxFileBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var buffer = new byte[MaxFileBytes + 1];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = await file.Content.ReadAsync(buffer.AsMemory(totalRead), cancellationToken)) > 0)
            totalRead += read;

        if (totalRead > MaxFileBytes)
            throw new McpException(
                $"Secure file exceeds the {MaxFileBytes}-byte limit this tool can return. Download it directly from GitLab instead.");

        var payload = new SecureFileDownload(
            file.ContentType,
            file.ContentLength,
            file.FileName,
            Convert.ToBase64String(buffer, 0, totalRead));

        return GitLabContent.Wrap(payload, "projects/:id/secure_files/:id/download");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_secure_file
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_secure_file", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Permanently deletes a secure file. GitLab answers 204 and the contents are unrecoverable afterward.")]
    public async Task<SecureFileDeleteResult> DeleteSecureFileAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The secure file's numeric id.")]
        long secureFileId,
        CancellationToken cancellationToken = default)
    {
        await secureFiles.DeleteAsync(project, secureFileId, cancellationToken);
        return new SecureFileDeleteResult(secureFileId, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_pages_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_pages_settings", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets a project's GitLab Pages configuration - where the site is served, whether HTTPS is forced, which domain is canonical, and what is currently deployed. Requires the Maintainer or Owner role on the project.")]
    public async Task<CallToolResult> GetPagesSettingsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var settings = await pages.GetSettingsAsync(project, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToSettingsSummary(settings), "projects/:id/pages");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_pages_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_pages_settings", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a project's GitLab Pages configuration. Only the fields supplied are changed; anything left unset keeps its current value. Requires the Maintainer or Owner role on the project.")]
    public async Task<CallToolResult> UpdatePagesSettingsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "If true, also serves this project's Pages site under GitLab's unique per-project domain. Omit to leave unchanged.")]
        bool? uniqueDomainEnabled = null,
        [Description("If true, requires HTTPS for this Pages site. Omit to leave unchanged.")]
        bool? forceHttps = null,
        [Description(
            "New primary (canonical) domain GitLab redirects the other configured domains to. Omit to leave unchanged.")]
        string? primaryDomain = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePagesSettingsRequest
        {
            PagesUniqueDomainEnabled = uniqueDomainEnabled,
            PagesHttpsOnly = forceHttps,
            PagesPrimaryDomain = primaryDomain
        };

        var settings = await pages.UpdateSettingsAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToSettingsSummary(settings), "projects/:id/pages (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_unpublish_pages
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_unpublish_pages", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Unpublishes a project's deployed Pages site, deleting what is currently deployed. The project's Pages configuration and custom domains survive - a later pipeline republishes.")]
    public async Task<PagesUnpublishResult> UnpublishPagesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        await pages.UnpublishAsync(project, cancellationToken);
        return new PagesUnpublishResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_check_pages_access
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_check_pages_access", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Checks whether the caller may view a project's GitLab Pages site. Answers a plain true/false rather than an error - false covers a private site the caller cannot see, a project the caller cannot see at all, and a project with no Pages site configured.")]
    public async Task<PagesAccessResult> CheckPagesAccessAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await pages.CheckAccessAsync(project, cancellationToken);
            return new PagesAccessResult(true);
        }
        catch (GitLabForbiddenException)
        {
            return new PagesAccessResult(false);
        }
        catch (GitLabNotFoundException)
        {
            // The project does not exist, is invisible to the configured token, or has no Pages site --
            // all read as "cannot view" for this yes/no check, not an error to surface. Any other
            // GitLabApiException propagates to the cross-cutting mapper.
            return new PagesAccessResult(false);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_pages_domains
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_pages_domains", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the custom domains configured on one project's GitLab Pages site, with each domain's DNS verification status and certificate expiry. Never returns the certificate itself.")]
    public async Task<CallToolResult> ListPagesDomainsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum domains to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<PagesDomainDetail> collected = [];
        var truncated = false;
        await foreach (var domain in pages.ListDomainsAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PagesMapper.ToDetail(domain));
        }

        return GitLabContent.Wrap(new ProjectPagesDomainListResult(collected, truncated), "projects/:id/pages/domains");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_pages_domain
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_pages_domain", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one custom domain configured on a project's GitLab Pages site, by hostname. Never returns the certificate itself - only its subject and expiry.")]
    public async Task<CallToolResult> GetPagesDomainAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The domain's exact hostname, e.g. \"docs.example.com\".")]
        string domain,
        CancellationToken cancellationToken = default)
    {
        var found = await pages.GetDomainAsync(project, domain, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToDetail(found), "projects/:id/pages/domains/:domain");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_pages_domain
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_pages_domain", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Rotates the TLS certificate on an existing Pages domain, or switches it to a Let's Encrypt-managed certificate. The hostname itself cannot be changed this way - delete the domain and add it again under the new hostname instead.")]
    public async Task<CallToolResult> UpdatePagesDomainAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The domain's exact hostname to update, e.g. \"docs.example.com\".")]
        string domain,
        [Description("New PEM-encoded TLS certificate. Omit if only switching autoSslEnabled.")]
        string? certificate = null,
        [Description("New PEM-encoded private key for the certificate. Omit if only switching autoSslEnabled.")]
        string? key = null,
        [Description(
            "If true, switches this domain to a Let's Encrypt-managed certificate instead of the supplied certificate/key. Omit to leave unchanged.")]
        bool? autoSslEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePagesDomainRequest
        {
            Certificate = certificate,
            Key = key,
            AutoSslEnabled = autoSslEnabled
        };

        var updated = await pages.UpdateDomainAsync(project, domain, request, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToDetail(updated), "projects/:id/pages/domains/:domain (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_pages_domain
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_pages_domain", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Permanently removes a custom domain from a project's GitLab Pages site. The site remains reachable at GitLab's own generated domain; only this custom domain mapping is removed.")]
    public async Task<PagesDomainDeleteResult> DeletePagesDomainAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The domain's exact hostname to remove, e.g. \"docs.example.com\".")]
        string domain,
        CancellationToken cancellationToken = default)
    {
        await pages.DeleteDomainAsync(project, domain, cancellationToken);
        return new PagesDomainDeleteResult(domain, true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_verify_pages_domain
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_verify_pages_domain", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Asks GitLab to re-check a Pages domain's DNS verification TXT record right now, instead of waiting for the next scheduled check. A failed check is not an error - read the result's Verified field to see whether it succeeded.")]
    public async Task<CallToolResult> VerifyPagesDomainAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The domain's exact hostname to verify, e.g. \"docs.example.com\".")]
        string domain,
        CancellationToken cancellationToken = default)
    {
        var result = await pages.VerifyDomainAsync(project, domain, cancellationToken);
        return GitLabContent.Wrap(PagesMapper.ToDetail(result), "projects/:id/pages/domains/:domain/verify");
    }

    // ---------------------------------------------------------------------------------------------
    // Validation helpers
    // ---------------------------------------------------------------------------------------------

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");
    }

    private static void ValidateOptionalLimit(int? limit)
    {
        if (limit is < 1 or > MaxLimit)
            throw new McpException($"limit must be between 1 and {MaxLimit}, or omitted.");
    }

    private static void ValidateSort(string? sort)
    {
        if (sort is not (null or "" or "asc" or "desc"))
            throw new McpException("sort must be \"asc\", \"desc\", or omitted.");
    }

    private static void ValidateRunnerType(string? type)
    {
        if (type is not (null or "" or "instance_type" or "group_type" or "project_type"))
            throw new McpException("type must be \"instance_type\", \"group_type\", \"project_type\", or omitted.");
    }

    private static void ValidateRunnerStatus(string? status)
    {
        if (status is not (null or "" or "active" or "paused" or "online" or "offline" or "never_contacted" or "stale"))
            throw new McpException(
                "status must be \"active\", \"paused\", \"online\", \"offline\", \"never_contacted\", \"stale\", or omitted.");
    }

    private static void ValidateAccessLevel(string? accessLevel)
    {
        if (accessLevel is not (null or "" or "not_protected" or "ref_protected"))
            throw new McpException("accessLevel must be \"not_protected\", \"ref_protected\", or omitted.");
    }

    private static void ValidateJobStatus(string? status)
    {
        if (status is not (null or "" or "created" or "pending" or "running" or "failed" or "success"
            or "canceled" or "skipped" or "waiting_for_resource" or "manual"))
            throw new McpException(
                "status must be \"created\", \"pending\", \"running\", \"failed\", \"success\", \"canceled\", " +
                "\"skipped\", \"waiting_for_resource\", \"manual\", or omitted.");
    }

    private static GitLabRunnerControllerState? ParseControllerState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "enabled" => GitLabRunnerControllerState.Enabled,
            "disabled" => GitLabRunnerControllerState.Disabled,
            "dry_run" => GitLabRunnerControllerState.DryRun,
            _ => throw new McpException("state must be \"enabled\", \"disabled\", \"dry_run\", or omitted.")
        };
    }

    private static void ValidateTier(string? tier)
    {
        if (tier is not (null or "" or "production" or "staging" or "testing" or "development" or "other"))
            throw new McpException(
                "tier must be \"production\", \"staging\", \"testing\", \"development\", \"other\", or omitted.");
    }

    private static void ValidateEnvironmentState(string? state)
    {
        if (state is not (null or "" or "available" or "stopping" or "stopped"))
            throw new McpException("state must be \"available\", \"stopping\", \"stopped\", or omitted.");
    }

    private static void ValidateDeploymentFilterStatus(string? status)
    {
        if (status is not (null or "" or "created" or "running" or "success" or "failed" or "canceled" or "skipped"
            or "blocked"))
            throw new McpException(
                "status must be \"created\", \"running\", \"success\", \"failed\", \"canceled\", \"skipped\", \"blocked\", or omitted.");
    }

    private static void ValidateDeploymentWriteStatus(string status)
    {
        if (status is not ("running" or "success" or "failed" or "canceled"))
            throw new McpException("status must be \"running\", \"success\", \"failed\", or \"canceled\".");
    }

    private static void ValidateDeploymentOrderBy(string? orderBy)
    {
        if (orderBy is not (null or "" or "id" or "iid" or "created_at" or "updated_at" or "finished_at"))
            throw new McpException(
                "orderBy must be \"id\", \"iid\", \"created_at\", \"updated_at\", \"finished_at\", or omitted.");
    }

    private static void ValidateApprovalStatus(string status)
    {
        if (status is not ("approved" or "rejected"))
            throw new McpException("status must be \"approved\" or \"rejected\".");
    }

    private static void ValidateProjectXorGroup(string? project, string? group)
    {
        var hasProject = !string.IsNullOrWhiteSpace(project);
        var hasGroup = !string.IsNullOrWhiteSpace(group);
        if (hasProject == hasGroup) throw new McpException("exactly one of project or group must be set.");
    }

    private static IReadOnlyList<string>? ParseCommaList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var items = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return items.Length == 0 ? null : items;
    }

    private static DateTimeOffset? ParseTimestamp(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        throw new McpException($"{paramName} must be an ISO 8601 timestamp (e.g. \"2026-09-09T00:00:00Z\").");
    }

    private static Uri? ParseAbsoluteUri(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)) return uri;

        throw new McpException($"{paramName} must be an absolute URL.");
    }

    private static IReadOnlyList<long>? ParseCommaLongList(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var ids = new long[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!long.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out ids[i]))
                throw new McpException($"{paramName} must be a comma-separated list of numeric ids.");

        return ids;
    }

    private static GitLabReleaseLinkType? ParseReleaseLinkType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "other" => GitLabReleaseLinkType.Other,
            "runbook" => GitLabReleaseLinkType.Runbook,
            "image" => GitLabReleaseLinkType.Image,
            "package" => GitLabReleaseLinkType.Package,
            _ => throw new McpException("linkType must be \"other\", \"runbook\", \"image\", \"package\", or omitted.")
        };
    }

    /// <summary>
    ///     Parses the JSON body GitLab's undocumented latest-release permalink returns (its own doc: "The
    ///     spec declares no response schema for this permalink route, so the raw body is returned rather
    ///     than an invented shape"). Reads GitLab's real snake_case field names directly with
    ///     <see cref="JsonDocument" /> rather than the library's camelCase-serialized model shape, since this
    ///     body never passes through the library's own deserializer.
    /// </summary>
    private static LatestReleaseSummary ParseLatestReleaseJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            return new LatestReleaseSummary(
                GetJsonString(root, "tag_name"),
                GetJsonString(root, "name"),
                GetJsonString(root, "description"),
                GetJsonTimestamp(root, "released_at"),
                root.TryGetProperty("author", out var author) ? GetJsonString(author, "username") : null,
                root.TryGetProperty("upcoming_release", out var upcoming)
                && upcoming.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? upcoming.GetBoolean()
                    : null,
                root.TryGetProperty("_links", out var links) ? GetJsonString(links, "self") : null);
        }
        catch (JsonException)
        {
            throw new McpException("gitlab_get_latest_release: GitLab's response could not be parsed as JSON.");
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTimeOffset? GetJsonTimestamp(JsonElement element, string propertyName)
    {
        return GetJsonString(element, propertyName) is { } raw
               && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}