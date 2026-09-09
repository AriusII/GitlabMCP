using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using GitLab.Client.Abstractions;
using GitLab.Client.Abstractions.Exceptions;
using GitLab.Client.Domain;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.Admin;
using GitlabMCP.Mapping.Admin;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     Instance administration, webhooks, integrations and audit tools: appearance branding, feature flags,
///     license policies, group integrations, background migrations, the audit log, project/group/system
///     webhooks, OAuth application registration, broadcast messages, Sidekiq queue administration, and
///     Service Ping. Built directly on <c>GitLab.Client</c>. Every tool returning GitLab-authored text wraps
///     via <see cref="GitLabContent" />. <see cref="ExchangePlatformTokenAsync" /> and
///     <see cref="CreateOAuthApplicationAsync" />/<see cref="CreateMyOAuthApplicationAsync" /> are the
///     plaintext-credential exceptions to the "never project a secret-bearing field" rule, under the same
///     carve-out PeopleTools.cs documents for token creation elsewhere: minting and returning the
///     JWT/OAuth-secret once is each tool's entire purpose, so it is still wrapped like any other GitLab-text
///     payload -- the exception is to what gets projected, not to whether it is wrapped.
/// </summary>
[McpServerToolType]
public sealed class AdminTools(
    IInstanceClient instance,
    IFeatureFlagsClient featureFlags,
    IFeaturesClient features,
    ILicensesClient licenses,
    IIntegrationsClient integrations,
    IPlatformIntegrationsClient platformIntegrations,
    IBackgroundMigrationsClient backgroundMigrations,
    IAuditEventsClient auditEvents,
    IProjectHooksClient projectHooks,
    IGroupHooksClient groupHooks,
    ISystemHooksClient systemHooks,
    IApplicationsClient applications,
    IBroadcastMessagesClient broadcastMessages,
    ISidekiqClient sidekiq,
    IUsageDataClient usageData,
    IAdminMigrationsClient adminMigrations,
    IInternalClient internalClient,
    IComplianceSettingsClient complianceSettings)
{
    private const int MaxLimit = 100;
    private const int MaxScriptChars = 200_000;
    private const int MaxJsonPayloadChars = 200_000;

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_instance_appearance
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_instance_appearance", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the GitLab instance's sign-in/sign-up branding: title, description, logo/favicon/PWA icon URLs, welcome-page guidelines, and header/footer message text and colors. Requires instance administrator access.")]
    public async Task<CallToolResult> GetInstanceAppearanceAsync(CancellationToken cancellationToken)
    {
        var appearance = await instance.GetAppearanceAsync(cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(appearance), "application/appearance");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_feature_flag_user_lists
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_feature_flag_user_lists", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a project's feature flag user lists -- named groups of user ids that a feature flag strategy can target.")]
    public async Task<CallToolResult> ListFeatureFlagUserListsAsync(
        [Description("Project: numeric id or \"namespace/path\", e.g. \"42\" or \"my-group/my-project\".")]
        string project,
        [Description("Optional free-text filter over the user list's name.")]
        string? search = null,
        [Description("Maximum user lists to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new FeatureFlagUserListOptions { Search = search, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<FeatureFlagUserListSummary> collected = [];
        var truncated = false;
        await foreach (var userList in featureFlags.ListUserListsAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(userList));
        }

        return GitLabContent.Wrap(new FeatureFlagUserListListResult(collected, truncated),
            "projects/:id/feature_flags_user_lists");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_license_policies
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_license_policies", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's software license compliance policies: each entry names a license and the allow/deny verdict recorded for it.")]
    public async Task<CallToolResult> ListLicensePoliciesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum policies to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new ManagedLicenseListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<LicensePolicySummary> collected = [];
        var truncated = false;
        await foreach (var license in licenses.ListManagedAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(license));
        }

        return GitLabContent.Wrap(new LicensePolicyListResult(collected, truncated), "projects/:id/managed_licenses");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_group_integrations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_integrations", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the third-party integrations (Slack, Jira, webhooks-as-a-service, etc.) active on a group. Integration-specific settings values are never included, only identifying metadata.")]
    public async Task<CallToolResult> ListGroupIntegrationsAsync(
        [Description("Group: numeric id or \"namespace/path\", e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Maximum integrations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<IntegrationSummary> collected = [];
        var truncated = false;
        await foreach (var integration in integrations.ListForGroupAsync(group, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(integration));
        }

        return GitLabContent.Wrap(new IntegrationListResult(collected, truncated), "groups/:id/integrations");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_background_migration_operations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_background_migration_operations", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists batched background operations on the instance -- the finer-grained unit a batched background migration is made of. Requires instance administrator access.")]
    public async Task<CallToolResult> ListBackgroundMigrationOperationsAsync(
        [Description(
            "Optional filter by Rails database: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        [Description("Optional filter by the operation's background job class name.")]
        string? jobClassName = null,
        [Description("Maximum operations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new BatchedBackgroundOperationListOptions
        {
            Database = ParseDatabase(database),
            JobClassName = jobClassName
        };

        List<BackgroundMigrationOperationSummary> collected = [];
        var truncated = false;
        await foreach (var operation in backgroundMigrations.ListOperationsAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(operation));
        }

        return GitLabContent.Wrap(new BackgroundMigrationOperationListResult(collected, truncated),
            "admin/batched_background_operations");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_audit_events
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_audit_events", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the instance-wide audit log, newest first, optionally filtered by entity type/id or a creation-date range. Requires instance administrator access. Event-specific detail payloads are never included, only the identifying fields.")]
    public async Task<CallToolResult> ListAuditEventsAsync(
        [Description(
            "Optional filter: the audited entity's type, e.g. \"User\", \"Project\", \"Group\". Requires entityId when set.")]
        string? entityType = null,
        [Description("Optional filter: the audited entity's numeric id. Requires entityType when set.")]
        long? entityId = null,
        [Description(
            "Optional filter: only events created at or after this instant. ISO 8601 (e.g. \"2025-01-01\" or \"2025-01-01T00:00:00Z\").")]
        string? createdAfter = null,
        [Description(
            "Optional filter: only events created at or before this instant. ISO 8601 (e.g. \"2025-01-31\" or \"2025-01-31T23:59:59Z\").")]
        string? createdBefore = null,
        [Description("Maximum events to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        if (entityType is null != entityId is null)
            throw new McpException("entityType and entityId must be specified together, or both omitted.");

        var options = new AuditEventListOptions
        {
            EntityType = entityType,
            EntityId = entityId,
            CreatedAfter = ParseInstant(createdAfter, nameof(createdAfter)),
            CreatedBefore = ParseInstant(createdBefore, nameof(createdBefore)),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<AuditEventSummary> collected = [];
        var truncated = false;
        await foreach (var auditEvent in auditEvents.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(auditEvent));
        }

        return GitLabContent.Wrap(new AuditEventListResult(collected, truncated), "audit_events");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_project_hook_deliveries
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_project_hook_deliveries", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Lists a project webhook's delivery log, newest first, for debugging failed deliveries. GitLab retains entries for 7 days only, so an empty result means nothing fired recently rather than nothing ever did. Response bodies are truncated at 500 characters.")]
    public async Task<CallToolResult> ListProjectHookDeliveriesAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id (from gitlab_create_project_hook or the project's webhook settings).")]
        long hookId,
        [Description(
            "Optional comma-separated filter on the response: exact HTTP status codes (\"200\", \"500\") and/or the buckets \"successful\", \"client_failure\", \"server_failure\". Omit for all.")]
        string? status = null,
        [Description("Maximum deliveries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new HookEventListOptions
            { Status = ParseStatusList(status), PerPage = Math.Min(limit + 1, MaxLimit) };

        List<HookDeliverySummary> collected = [];
        var truncated = false;
        await foreach (var delivery in projectHooks.ListEventsAsync(project, hookId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(delivery));
        }

        return GitLabContent.Wrap(new HookDeliveryListResult(collected, truncated),
            "projects/:id/hooks/:hook_id/events");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_group_hook_deliveries
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_hook_deliveries", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a group webhook's delivery log, newest first. GitLab retains entries for 7 days only, so an empty result means nothing fired recently rather than nothing ever did. Response bodies are truncated at 500 characters.")]
    public async Task<CallToolResult> ListGroupHookDeliveriesAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id (from gitlab_create_group_hook or the group's webhook settings).")]
        long hookId,
        [Description(
            "Optional comma-separated filter on the response: exact HTTP status codes (\"200\", \"500\") and/or the buckets \"successful\", \"client_failure\", \"server_failure\". Omit for all.")]
        string? status = null,
        [Description("Maximum deliveries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new HookEventListOptions
            { Status = ParseStatusList(status), PerPage = Math.Min(limit + 1, MaxLimit) };

        List<HookDeliverySummary> collected = [];
        var truncated = false;
        await foreach (var delivery in groupHooks.ListEventsAsync(group, hookId, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(delivery));
        }

        return GitLabContent.Wrap(new HookDeliveryListResult(collected, truncated), "groups/:id/hooks/:hook_id/events");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_feature_flag
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_feature_flag", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Creates a project feature flag, optionally with a single strategy already attached.")]
    public async Task<CallToolResult> CreateFeatureFlagAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The flag's name -- becomes its stable identifier, checked at evaluation time.")]
        string name,
        [Description("Optional human-readable description.")]
        string? description = null,
        [Description("Whether the flag starts active. Default true.")]
        bool active = true,
        [Description(
            "Optional strategy type to attach, e.g. \"default\", \"gradualRolloutUserId\", \"userWithId\", \"gitlabUserList\", \"flexibleRollout\". Omit to create the flag with no strategy.")]
        string? strategyName = null,
        [Description(
            "Optional environment scope for the attached strategy, e.g. \"production\" or \"*\" for all environments. Only used when strategyName is set; defaults to \"*\".")]
        string? environmentScope = null,
        [Description(
            "Optional strategy-specific parameters as a JSON object string, e.g. {\"percentage\":\"50\"} for gradualRolloutUserId. Only used when strategyName is set.")]
        string? strategyParametersJson = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FeatureFlagStrategyRequest>? strategies = null;
        if (strategyName is not null)
        {
            var scope = new FeatureFlagStrategyScopeRequest { EnvironmentScope = environmentScope ?? "*" };
            strategies =
            [
                new FeatureFlagStrategyRequest
                {
                    Name = strategyName,
                    Parameters = strategyParametersJson is null
                        ? null
                        : ParseJsonElement(strategyParametersJson, nameof(strategyParametersJson)),
                    Scopes = [scope]
                }
            ];
        }

        var request = new CreateFeatureFlagRequest
        {
            Name = name,
            Description = description,
            Active = active,
            Strategies = strategies
        };

        var flag = await featureFlags.CreateAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(flag), "projects/:id/feature_flags (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_license_policy
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_license_policy", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Creates or updates the allow/deny verdict for a software license on a project, identified by license name. If a policy for that name already exists its verdict is updated in place; otherwise a new policy is created.")]
    public async Task<CallToolResult> SetLicensePolicyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "The license's name exactly as GitLab's license list spells it, e.g. \"MIT\" or \"GNU General Public License v3.0\".")]
        string name,
        [Description("The verdict to record: \"allowed\" or \"denied\".")]
        string approvalStatus,
        CancellationToken cancellationToken = default)
    {
        var status = ParseApprovalStatus(approvalStatus);

        GitLabManagedLicense license;
        try
        {
            _ = await licenses.GetManagedAsync(project, name, cancellationToken);
            license = await licenses.UpdateManagedAsync(project, name,
                new UpdateManagedLicenseRequest { ApprovalStatus = status }, cancellationToken);
        }
        catch (GitLabNotFoundException)
        {
            // No existing policy for this license name -- this is the upsert's "create" branch, not an
            // error to surface. Any other GitLabApiException propagates to the cross-cutting mapper.
            license = await licenses.CreateManagedAsync(project,
                new CreateManagedLicenseRequest { Name = name, ApprovalStatus = status }, cancellationToken);
        }

        return GitLabContent.Wrap(AdminMapper.ToSummary(license), "projects/:id/managed_licenses (upsert)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_project_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_project_hook", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a webhook to a project, selecting which event types it fires on. Omitted event toggles fall back to GitLab's own default (usually off) rather than being explicitly disabled. Covers the most commonly used event types; less common ones (confidential note events, emoji, milestones, feature flags, releases-adjacent access-token events) are not exposed by this tool.")]
    public async Task<CallToolResult> CreateProjectHookAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The URL GitLab will POST event payloads to.")]
        string url,
        [Description("Optional display name for the webhook.")]
        string? name = null,
        [Description("Optional description.")] string? description = null,
        [Description("Fire on push events. Omit for GitLab's default.")]
        bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit for no restriction.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit for GitLab's default (wildcard).")]
        string? branchFilterStrategy = null,
        [Description("Fire on issue events.")] bool? issuesEvents = null,
        [Description("Fire on confidential issue events.")]
        bool? confidentialIssuesEvents = null,
        [Description("Fire on merge request events.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire on tag push events.")]
        bool? tagPushEvents = null,
        [Description("Fire on comment (note) events.")]
        bool? noteEvents = null,
        [Description("Fire on confidential comment (note) events.")]
        bool? confidentialNoteEvents = null,
        [Description("Fire on CI/CD job events.")]
        bool? jobEvents = null,
        [Description("Fire on CI/CD pipeline events.")]
        bool? pipelineEvents = null,
        [Description("Fire on wiki page events.")]
        bool? wikiPageEvents = null,
        [Description("Fire on deployment events.")]
        bool? deploymentEvents = null,
        [Description("Fire on release events.")]
        bool? releasesEvents = null,
        [Description(
            "Optional secret token GitLab sends back as the X-Gitlab-Token header, so the receiver can authenticate deliveries. Write-only: it is never returned by any tool.")]
        string? secretToken = null,
        [Description(
            "Optional HMAC signing key (whsec_... form) used to compute the webhook-signature header. Write-only: it is never returned by any tool.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit for GitLab's default (enabled).")]
        bool? enableSslVerification = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateProjectHookRequest
        {
            Url = ParseUrl(url),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            IssuesEvents = issuesEvents,
            ConfidentialIssuesEvents = confidentialIssuesEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            TagPushEvents = tagPushEvents,
            NoteEvents = noteEvents,
            ConfidentialNoteEvents = confidentialNoteEvents,
            JobEvents = jobEvents,
            PipelineEvents = pipelineEvents,
            WikiPageEvents = wikiPageEvents,
            DeploymentEvents = deploymentEvents,
            ReleasesEvents = releasesEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification
        };

        var hook = await projectHooks.AddAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "projects/:id/hooks (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_group_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_group_hook", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a webhook to a group, selecting which event types it fires on. Omitted event toggles fall back to GitLab's own default (usually off) rather than being explicitly disabled. Covers the most commonly used event types; less common ones are not exposed by this tool.")]
    public async Task<CallToolResult> CreateGroupHookAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The URL GitLab will POST event payloads to.")]
        string url,
        [Description("Optional display name for the webhook.")]
        string? name = null,
        [Description("Optional description.")] string? description = null,
        [Description("Fire on push events.")] bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit for no restriction.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit for GitLab's default (wildcard).")]
        string? branchFilterStrategy = null,
        [Description("Fire on issue events.")] bool? issuesEvents = null,
        [Description("Fire on confidential issue events.")]
        bool? confidentialIssuesEvents = null,
        [Description("Fire on merge request events.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire on tag push events.")]
        bool? tagPushEvents = null,
        [Description("Fire on comment (note) events.")]
        bool? noteEvents = null,
        [Description("Fire on confidential comment (note) events.")]
        bool? confidentialNoteEvents = null,
        [Description("Fire on CI/CD job events.")]
        bool? jobEvents = null,
        [Description("Fire on CI/CD pipeline events.")]
        bool? pipelineEvents = null,
        [Description("Fire on wiki page events.")]
        bool? wikiPageEvents = null,
        [Description("Fire on deployment events.")]
        bool? deploymentEvents = null,
        [Description("Fire on release events.")]
        bool? releasesEvents = null,
        [Description("Fire when a subgroup is created or removed under this group.")]
        bool? subgroupEvents = null,
        [Description("Fire when a project is created or removed under this group.")]
        bool? projectEvents = null,
        [Description("Fire on group membership changes.")]
        bool? memberEvents = null,
        [Description(
            "Optional secret token GitLab sends back as the X-Gitlab-Token header. Write-only: it is never returned by any tool.")]
        string? secretToken = null,
        [Description(
            "Optional HMAC signing key (whsec_... form) used to compute the webhook-signature header. Write-only: it is never returned by any tool.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit for GitLab's default (enabled).")]
        bool? enableSslVerification = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateGroupHookRequest
        {
            Url = ParseUrl(url),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            IssuesEvents = issuesEvents,
            ConfidentialIssuesEvents = confidentialIssuesEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            TagPushEvents = tagPushEvents,
            NoteEvents = noteEvents,
            ConfidentialNoteEvents = confidentialNoteEvents,
            JobEvents = jobEvents,
            PipelineEvents = pipelineEvents,
            WikiPageEvents = wikiPageEvents,
            DeploymentEvents = deploymentEvents,
            ReleasesEvents = releasesEvents,
            SubgroupEvents = subgroupEvents,
            ProjectEvents = projectEvents,
            MemberEvents = memberEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification
        };

        var hook = await groupHooks.CreateAsync(group, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "groups/:id/hooks (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_system_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_system_hook", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Registers an instance-wide system hook that fires on events across every project and group (new users, new projects, pushes, merge requests, repository updates). Requires instance administrator access.")]
    public async Task<CallToolResult> CreateSystemHookAsync(
        [Description("The URL GitLab will POST event payloads to.")]
        string url,
        [Description("Optional display name for the hook.")]
        string? name = null,
        [Description("Optional description.")] string? description = null,
        [Description("Fire on push events across every project.")]
        bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit for no restriction.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit for GitLab's default (wildcard).")]
        string? branchFilterStrategy = null,
        [Description("Fire on tag push events across every project.")]
        bool? tagPushEvents = null,
        [Description("Fire on merge request events across every project.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire when any repository is updated.")]
        bool? repositoryUpdateEvents = null,
        [Description(
            "Optional secret token GitLab sends back as the X-Gitlab-Token header. Write-only: it is never returned by any tool.")]
        string? secretToken = null,
        [Description(
            "Optional HMAC signing key (whsec_... form) used to compute the webhook-signature header. Write-only: it is never returned by any tool.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit for GitLab's default (enabled).")]
        bool? enableSslVerification = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateSystemHookRequest
        {
            Url = ParseUrl(url),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            TagPushEvents = tagPushEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            RepositoryUpdateEvents = repositoryUpdateEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification
        };

        var hook = await systemHooks.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "hooks (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_group_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_group_integration", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Creates or fully replaces a group integration's settings by slug (e.g. \"slack\", \"jira\", \"datadog\"). This is a full replace, not a merge: any setting the integration supports but that settingsJson omits is cleared to its default. Settings values (API keys, webhook URLs, passwords) are write-only and never returned by any tool.")]
    public async Task<CallToolResult> SetGroupIntegrationAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description(
            "The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\". Matches the identifier GitLab's own integrations settings page uses in its URL.")]
        string slug,
        [Description(
            "The integration's settings as a JSON object string, e.g. {\"webhook\":\"https://hooks.example.com/...\",\"notify_only_broken_pipelines\":true}. Field names match GitLab's REST API for that integration.")]
        string settingsJson,
        CancellationToken cancellationToken = default)
    {
        var settings = ParseSettingsObject(settingsJson, nameof(settingsJson));
        var integration = await integrations.SetForGroupAsync(group, slug, settings, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(integration), "groups/:id/:slug (set)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_instance_appearance
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_instance_appearance", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates the GitLab instance's sign-in/sign-up branding text and toggle fields, leaving fields left unset unchanged. Image fields (logo, favicon, header logo, PWA icon) require a file upload and are not covered by this tool. Requires instance administrator access.")]
    public async Task<CallToolResult> UpdateInstanceAppearanceAsync(
        [Description("Instance title shown on the sign-in page. Omit to leave unchanged.")]
        string? title = null,
        [Description("Instance description shown on the sign-in page. Omit to leave unchanged.")]
        string? description = null,
        [Description("Progressive Web App name. Omit to leave unchanged.")]
        string? pwaName = null,
        [Description("Progressive Web App short name. Omit to leave unchanged.")]
        string? pwaShortName = null,
        [Description("Progressive Web App description. Omit to leave unchanged.")]
        string? pwaDescription = null,
        [Description("Guidance text shown on the new-project page. Omit to leave unchanged.")]
        string? newProjectGuidelines = null,
        [Description("Guidance text shown on the member-invitation page. Omit to leave unchanged.")]
        string? memberGuidelines = null,
        [Description("Guidance text shown next to a user's profile-image upload control. Omit to leave unchanged.")]
        string? profileImageGuidelines = null,
        [Description("Banner message shown at the top of every page. Omit to leave unchanged.")]
        string? headerMessage = null,
        [Description("Banner message shown at the bottom of every page. Omit to leave unchanged.")]
        string? footerMessage = null,
        [Description(
            "Background color (CSS color value) for the header/footer message banners. Omit to leave unchanged.")]
        string? messageBackgroundColor = null,
        [Description("Font color (CSS color value) for the header/footer message banners. Omit to leave unchanged.")]
        string? messageFontColor = null,
        [Description(
            "Whether the header/footer message banners also appear in outgoing notification emails. Omit to leave unchanged.")]
        bool? emailHeaderAndFooterEnabled = null,
        [Description("Site name shown in various instance-wide labels. Omit to leave unchanged.")]
        string? siteName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateApplicationAppearanceRequest
        {
            Title = title,
            Description = description,
            PwaName = pwaName,
            PwaShortName = pwaShortName,
            PwaDescription = pwaDescription,
            NewProjectGuidelines = newProjectGuidelines,
            MemberGuidelines = memberGuidelines,
            ProfileImageGuidelines = profileImageGuidelines,
            HeaderMessage = headerMessage,
            FooterMessage = footerMessage,
            MessageBackgroundColor = messageBackgroundColor,
            MessageFontColor = messageFontColor,
            EmailHeaderAndFooterEnabled = emailHeaderAndFooterEnabled,
            SiteName = siteName
        };

        var appearance = await instance.UpdateAppearanceAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(appearance), "application/appearance (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_instance_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_instance_settings", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the instance-wide GitLab application settings: visibility defaults, timeouts, security toggles, abuse/anti-spam controls, import concurrency limits, and related instance-level configuration. Secret-shaped fields (the Akismet API key, the static-objects storage auth token) are never included. Requires instance administrator access.")]
    public async Task<CallToolResult> GetInstanceSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await instance.GetSettingsAsync(cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(settings), "application/settings");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_instance_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_instance_settings", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes one or more instance-wide GitLab application settings; fields left unset (omitted) keep their current value. Covers visibility defaults, timeouts, and security toggles -- not every setting GitLab exposes. Requires instance administrator access.")]
    public async Task<CallToolResult> UpdateInstanceSettingsAsync(
        [Description(
            "Whether Admin Mode is required before an administrator's elevated permissions apply. Omit to leave unchanged.")]
        bool? adminMode = null,
        [Description(
            "Email address notified when a new sign-up is flagged for admin approval. Omit to leave unchanged.")]
        string? adminNotificationEmail = null,
        [Description("Email address notified when a user is reported for abuse. Omit to leave unchanged.")]
        string? abuseNotificationEmail = null,
        [Description("Text shown to a user immediately after they sign up. Omit to leave unchanged.")]
        string? afterSignUpText = null,
        [Description("Path GitLab redirects to after a sign-out. Omit to leave unchanged.")]
        string? afterSignOutPath = null,
        [Description("Whether Akismet spam checking is enabled. Omit to leave unchanged.")]
        bool? akismetEnabled = null,
        [Description(
            "Whether the asset proxy (for external image sources in Markdown) is enabled. Omit to leave unchanged.")]
        bool? assetProxyEnabled = null,
        [Description(
            "Absolute URL of the asset proxy server. Only used when assetProxyEnabled is set. Omit to leave unchanged.")]
        string? assetProxyUrl = null,
        [Description(
            "Whether cleanup of expired sign-in/authentication data-retention records runs automatically. Omit to leave unchanged.")]
        bool? authnDataRetentionCleanupEnabled = null,
        [Description("Seconds a generated container registry token stays valid. Omit to leave unchanged.")]
        int? containerRegistryTokenExpireDelay = null,
        [Description("Seconds an OAuth access token stays valid. Omit to leave unchanged.")]
        int? oauthAccessTokenExpiresIn = null,
        [Description(
            "Seconds allowed to decompress an uploaded archive file before GitLab aborts the request. Omit to leave unchanged.")]
        int? decompressArchiveFileTimeout = null,
        [Description(
            "Default expiry for job artifacts that don't set their own, e.g. \"30 days\". Omit to leave unchanged.")]
        string? defaultArtifactsExpireIn = null,
        [Description(
            "Default relative path GitLab looks for a project's CI/CD config file at, e.g. \".gitlab-ci.yml\". Omit to leave unchanged.")]
        string? defaultCiConfigPath = null,
        [Description("Default value (0-2) for who may create new projects on the instance. Omit to leave unchanged.")]
        int? defaultProjectCreation = null,
        [Description(
            "Default branch-protection level (0-4) applied to a new project's default branch. Omit to leave unchanged.")]
        int? defaultBranchProtection = null,
        [Description(
            "Default visibility for new groups: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? defaultGroupVisibility = null,
        [Description(
            "Default visibility for new projects: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? defaultProjectVisibility = null,
        [Description(
            "Default visibility for new snippets: \"private\", \"internal\", or \"public\". Omit to leave unchanged.")]
        string? defaultSnippetVisibility = null,
        [Description("Default maximum number of personal projects a new user may create. Omit to leave unchanged.")]
        int? defaultProjectsLimit = null,
        [Description(
            "Whether administrators must explicitly grant OAuth applications admin-scoped access. Omit to leave unchanged.")]
        bool? disableAdminOauthScopes = null,
        [Description("Whether a user's personal RSS/calendar feed token is disabled. Omit to leave unchanged.")]
        bool? disableFeedToken = null,
        [Description("Whether the sign-up domain denylist is enforced. Omit to leave unchanged.")]
        bool? domainDenylistEnabled = null,
        [Description("Whether one-time-password (email OTP) sign-in verification is enabled. Omit to leave unchanged.")]
        bool? emailOtpEnabled = null,
        [Description("Whether GitLab-authored content may be rendered inside an iframe. Omit to leave unchanged.")]
        bool? iframeRenderingEnabled = null,
        [Description(
            "Whether outgoing notification emails include the author's name in the body. Omit to leave unchanged.")]
        bool? emailAuthorInBody = null,
        [Description(
            "How a new user's email is confirmed: \"off\", \"soft\" (allowed to use the account before confirming), or \"hard\" (must confirm first). Omit to leave unchanged.")]
        string? emailConfirmationSetting = null,
        [Description(
            "Which Git access protocol(s) are enabled: \"ssh\", \"http\", or \"all\". Omit to leave unchanged.")]
        string? enabledGitAccessProtocol = null,
        [Description("Whether the Gitpod integration is enabled. Omit to leave unchanged.")]
        bool? gitpodEnabled = null,
        [Description("Default Gitaly RPC timeout in seconds. Omit to leave unchanged.")]
        int? gitalyTimeoutDefault = null,
        [Description("Gitaly RPC timeout in seconds for fast operations. Omit to leave unchanged.")]
        int? gitalyTimeoutFast = null,
        [Description("Gitaly RPC timeout in seconds for medium-length operations. Omit to leave unchanged.")]
        int? gitalyTimeoutMedium = null,
        [Description("Whether the bundled Grafana instance is enabled. Omit to leave unchanged.")]
        bool? grafanaEnabled = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateApplicationSettingsRequest
        {
            AdminMode = adminMode,
            AdminNotificationEmail = adminNotificationEmail,
            AbuseNotificationEmail = abuseNotificationEmail,
            AfterSignUpText = afterSignUpText,
            AfterSignOutPath = afterSignOutPath,
            AkismetEnabled = akismetEnabled,
            AssetProxyEnabled = assetProxyEnabled,
            AssetProxyUrl = ParseUriOptional(assetProxyUrl, nameof(assetProxyUrl)),
            AuthnDataRetentionCleanupEnabled = authnDataRetentionCleanupEnabled,
            ContainerRegistryTokenExpireDelay = containerRegistryTokenExpireDelay,
            OauthAccessTokenExpiresIn = oauthAccessTokenExpiresIn,
            DecompressArchiveFileTimeout = decompressArchiveFileTimeout,
            DefaultArtifactsExpireIn = defaultArtifactsExpireIn,
            DefaultCiConfigPath = defaultCiConfigPath,
            DefaultProjectCreation = defaultProjectCreation,
            DefaultBranchProtection = defaultBranchProtection,
            DefaultGroupVisibility = ParseVisibility(defaultGroupVisibility, nameof(defaultGroupVisibility)),
            DefaultProjectVisibility = ParseVisibility(defaultProjectVisibility, nameof(defaultProjectVisibility)),
            DefaultSnippetVisibility = ParseVisibility(defaultSnippetVisibility, nameof(defaultSnippetVisibility)),
            DefaultProjectsLimit = defaultProjectsLimit,
            DisableAdminOauthScopes = disableAdminOauthScopes,
            DisableFeedToken = disableFeedToken,
            DomainDenylistEnabled = domainDenylistEnabled,
            EmailOtpEnabled = emailOtpEnabled,
            IframeRenderingEnabled = iframeRenderingEnabled,
            EmailAuthorInBody = emailAuthorInBody,
            EmailConfirmationSetting =
                ParseEmailConfirmationSetting(emailConfirmationSetting, nameof(emailConfirmationSetting)),
            EnabledGitAccessProtocol =
                ParseGitAccessProtocol(enabledGitAccessProtocol, nameof(enabledGitAccessProtocol)),
            GitpodEnabled = gitpodEnabled,
            GitalyTimeoutDefault = gitalyTimeoutDefault,
            GitalyTimeoutFast = gitalyTimeoutFast,
            GitalyTimeoutMedium = gitalyTimeoutMedium,
            GrafanaEnabled = grafanaEnabled
        };

        var settings = await instance.UpdateSettingsAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(settings), "application/settings (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_instance_statistics
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_instance_statistics", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets instance-wide entity counts: projects, groups, users, active users, issues, merge requests, notes, snippets, SSH keys and milestones. Requires instance administrator access.")]
    public async Task<InstanceStatisticsSummary> GetInstanceStatisticsAsync(CancellationToken cancellationToken)
    {
        var statistics = await instance.GetStatisticsAsync(cancellationToken);
        return AdminMapper.ToSummary(statistics);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_instance_metadata
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_instance_metadata", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the GitLab instance's version, revision and edition (Enterprise or Community), plus whether the GitLab Agent Server for Kubernetes (KAS) is enabled. Unlike every other tool on this client, this one does not require administrator access -- any authenticated token can call it.")]
    public async Task<CallToolResult> GetInstanceMetadataAsync(CancellationToken cancellationToken)
    {
        var metadata = await instance.GetMetadataAsync(cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(metadata), "metadata");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_plan_limits
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_plan_limits", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the resource limits (CI job counts, package max file sizes, storage, notification/web-hook call limits) configured for a billing plan on a self-managed instance. Requires instance administrator access.")]
    public async Task<PlanLimitsSummary> GetPlanLimitsAsync(
        [Description(
            "Optional plan to query, e.g. \"free\", \"premium\", \"ultimate\", \"default\", \"bronze\", \"silver\", \"gold\", \"opensource\". Omit to query the instance's default plan.")]
        string? planName = null,
        CancellationToken cancellationToken = default)
    {
        PlanLimitsOptions? options = string.IsNullOrWhiteSpace(planName)
            ? null
            : new PlanLimitsOptions { PlanName = ParsePlanName(planName, nameof(planName)) };

        var limits = await instance.GetPlanLimitsAsync(options, cancellationToken);
        return AdminMapper.ToSummary(limits);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_plan_limits
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_plan_limits", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes one or more resource limits configured for a billing plan; limits left unset (omitted) keep their current value. Requires instance administrator access.")]
    public async Task<PlanLimitsSummary> UpdatePlanLimitsAsync(
        [Description(
            "The plan to change: \"free\", \"premium\", \"ultimate\", \"default\", \"bronze\", \"silver\", \"gold\", \"opensource\", \"ultimate_trial\", \"premium_trial\", or \"ultimate_trial_paid_customer\".")]
        string planName,
        [Description("Max size in bytes of an uploaded Cargo (Rust) package file. Omit to leave unchanged.")]
        long? cargoMaxFileSize = null,
        [Description("Max number of instance-level CI/CD variables. Omit to leave unchanged.")]
        long? ciInstanceLevelVariables = null,
        [Description("Max size in bytes of a single pipeline's CI/CD configuration. Omit to leave unchanged.")]
        long? ciPipelineSize = null,
        [Description("Max number of jobs that may be active (running or pending) at once. Omit to leave unchanged.")]
        long? ciActiveJobs = null,
        [Description(
            "Max number of projects a single pipeline may trigger downstream subscriptions to. Omit to leave unchanged.")]
        long? ciProjectSubscriptions = null,
        [Description("Max number of pipeline schedules per project. Omit to leave unchanged.")]
        long? ciPipelineSchedules = null,
        [Description("Max total size in bytes of a job's \"needs\" dependency list. Omit to leave unchanged.")]
        long? ciNeedsSizeLimit = null,
        [Description("Max number of runners a single group may register. Omit to leave unchanged.")]
        long? ciRegisteredGroupRunners = null,
        [Description("Max number of runners a single project may register. Omit to leave unchanged.")]
        long? ciRegisteredProjectRunners = null,
        [Description("Max size in bytes of an uploaded Conan package file. Omit to leave unchanged.")]
        long? conanMaxFileSize = null,
        [Description("Max number of variables a job may load from a dotenv artifact. Omit to leave unchanged.")]
        long? dotenvVariables = null,
        [Description("Max size in bytes of a dotenv artifact a job may load. Omit to leave unchanged.")]
        long? dotenvSize = null,
        [Description(
            "Max value GitLab enforces before blocking further use of the resource this limit is enforcing. Omit to leave unchanged.")]
        long? enforcementLimit = null,
        [Description("Max size in bytes of an uploaded generic package file. Omit to leave unchanged.")]
        long? genericPackagesMaxFileSize = null,
        [Description("Max size in bytes of an uploaded Helm chart file. Omit to leave unchanged.")]
        long? helmMaxFileSize = null,
        [Description("Max size in bytes of an uploaded Maven package file. Omit to leave unchanged.")]
        long? mavenMaxFileSize = null,
        [Description(
            "Max number of unread notifications retained before older ones are dropped. Omit to leave unchanged.")]
        long? notificationLimit = null,
        [Description("Max size in bytes of an uploaded npm package file. Omit to leave unchanged.")]
        long? npmMaxFileSize = null,
        [Description("Max size in bytes of an uploaded NuGet package file. Omit to leave unchanged.")]
        long? nugetMaxFileSize = null,
        [Description("Max depth of a multi-project (parent/child) pipeline hierarchy. Omit to leave unchanged.")]
        long? pipelineHierarchySize = null,
        [Description("Max size in bytes of an uploaded PyPI package file. Omit to leave unchanged.")]
        long? pypiMaxFileSize = null,
        [Description("Max Service Desk outbound emails per hour. Omit to leave unchanged.")]
        long? serviceDeskOutboundEmailsPerHour = null,
        [Description("Max Service Desk outbound emails per day. Omit to leave unchanged.")]
        long? serviceDeskOutboundEmailsPerDay = null,
        [Description("Max size in bytes of an uploaded Terraform module package file. Omit to leave unchanged.")]
        long? terraformModuleMaxFileSize = null,
        [Description("Max total repository storage size in bytes. Omit to leave unchanged.")]
        long? storageSizeLimit = null,
        [Description("Max total outbound web-hook calls. Omit to leave unchanged.")]
        long? webHookCalls = null,
        [Description("Max outbound web-hook calls at the low-urgency priority tier. Omit to leave unchanged.")]
        long? webHookCallsLow = null,
        [Description("Max outbound web-hook calls at the mid-urgency priority tier. Omit to leave unchanged.")]
        long? webHookCallsMid = null,
        [Description(
            "Max number of pipelines that may run concurrently in a single merge train. Omit to leave unchanged.")]
        long? maxPipelinesPerMergeTrain = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePlanLimitsRequest
        {
            PlanName = ParsePlanName(planName, nameof(planName)),
            CargoMaxFileSize = cargoMaxFileSize,
            CiInstanceLevelVariables = ciInstanceLevelVariables,
            CiPipelineSize = ciPipelineSize,
            CiActiveJobs = ciActiveJobs,
            CiProjectSubscriptions = ciProjectSubscriptions,
            CiPipelineSchedules = ciPipelineSchedules,
            CiNeedsSizeLimit = ciNeedsSizeLimit,
            CiRegisteredGroupRunners = ciRegisteredGroupRunners,
            CiRegisteredProjectRunners = ciRegisteredProjectRunners,
            ConanMaxFileSize = conanMaxFileSize,
            DotenvVariables = dotenvVariables,
            DotenvSize = dotenvSize,
            EnforcementLimit = enforcementLimit,
            GenericPackagesMaxFileSize = genericPackagesMaxFileSize,
            HelmMaxFileSize = helmMaxFileSize,
            MavenMaxFileSize = mavenMaxFileSize,
            NotificationLimit = notificationLimit,
            NpmMaxFileSize = npmMaxFileSize,
            NugetMaxFileSize = nugetMaxFileSize,
            PipelineHierarchySize = pipelineHierarchySize,
            PypiMaxFileSize = pypiMaxFileSize,
            ServiceDeskOutboundEmailsPerHour = serviceDeskOutboundEmailsPerHour,
            ServiceDeskOutboundEmailsPerDay = serviceDeskOutboundEmailsPerDay,
            TerraformModuleMaxFileSize = terraformModuleMaxFileSize,
            StorageSizeLimit = storageSizeLimit,
            WebHookCalls = webHookCalls,
            WebHookCallsLow = webHookCallsLow,
            WebHookCallsMid = webHookCallsMid,
            MaxPipelinesPerMergeTrain = maxPipelinesPerMergeTrain
        };

        var limits = await instance.UpdatePlanLimitsAsync(request, cancellationToken);
        return AdminMapper.ToSummary(limits);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_current_license
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_current_license", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the license currently activating this self-managed instance, including its billable user count. Returns nothing meaningful on GitLab.com or an unlicensed Community Edition instance. Requires instance administrator access.")]
    public async Task<CallToolResult> GetCurrentLicenseAsync(CancellationToken cancellationToken)
    {
        var license = await licenses.GetCurrentAsync(cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(license), "license");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_licenses
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_licenses", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every license the instance has ever had. Entries omit the billable-user count; call gitlab_get_license for that. Requires instance administrator access.")]
    public async Task<CallToolResult> ListLicensesAsync(
        [Description("Maximum licenses to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<LicenseSummary> collected = [];
        var truncated = false;
        await foreach (var license in licenses.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(license));
        }

        return GitLabContent.Wrap(new LicenseListResult(collected, truncated), "licenses");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_license
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_license", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one license by id, including its billable user count. Requires instance administrator access.")]
    public async Task<CallToolResult> GetLicenseAsync(
        [Description("The license's numeric id, from gitlab_list_licenses or gitlab_get_current_license.")]
        long licenseId,
        CancellationToken cancellationToken = default)
    {
        var license = await licenses.GetAsync(licenseId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(license), "licenses/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_activate_license
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_activate_license", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Uploads and activates a license key on the instance. The key is as sensitive as an access token: it is sent straight to GitLab and never echoed back by this or any other tool. Requires instance administrator access.")]
    public async Task<CallToolResult> ActivateLicenseAsync(
        [Description("The license key, exactly as GitLab issued it.")]
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var license = await licenses.CreateAsync(new CreateLicenseRequest { License = licenseKey }, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(license), "license (activate)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_license
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_license", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a license from the instance. Deleting the currently active license immediately deactivates Enterprise Edition features. Requires instance administrator access.")]
    public async Task<LicenseDeleteResult> DeleteLicenseAsync(
        [Description("The license's numeric id.")]
        long licenseId,
        CancellationToken cancellationToken = default)
    {
        await licenses.DeleteAsync(licenseId, cancellationToken);
        return new LicenseDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_refresh_license_billable_users
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_refresh_license_billable_users", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Triggers GitLab to recalculate a license's billable user count. Requires instance administrator access.")]
    public async Task<LicenseRefreshResult> RefreshLicenseBillableUsersAsync(
        [Description("The license's numeric id.")]
        long licenseId,
        CancellationToken cancellationToken = default)
    {
        var result = await licenses.RefreshBillableUsersAsync(licenseId, cancellationToken);
        return AdminMapper.ToSummary(result);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_license_policy
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_license_policy", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Removes a software license compliance policy from a project, identified by license name.")]
    public async Task<LicensePolicyDeleteResult> DeleteLicensePolicyAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "The license's name exactly as recorded by gitlab_list_license_policies or gitlab_set_license_policy, e.g. \"MIT\".")]
        string name,
        CancellationToken cancellationToken = default)
    {
        await licenses.DeleteManagedAsync(project, name, cancellationToken);
        return new LicensePolicyDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_audit_event
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_audit_event", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one instance-wide audit log entry by id. Requires instance administrator access.")]
    public async Task<CallToolResult> GetAuditEventAsync(
        [Description("The audit event's numeric id, from gitlab_list_audit_events.")]
        long auditEventId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = await auditEvents.GetAsync(auditEventId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(auditEvent), "audit_events/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_group_audit_events
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_audit_events", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a group's audit log, newest first, optionally filtered by a creation-date range. Requires the Owner role on the group, or instance administrator access. Event-specific detail payloads are never included, only the identifying fields.")]
    public async Task<CallToolResult> ListGroupAuditEventsAsync(
        [Description("Group: numeric id or \"namespace/path\", e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description(
            "Optional filter: only events created at or after this instant. ISO 8601 (e.g. \"2025-01-01\" or \"2025-01-01T00:00:00Z\").")]
        string? createdAfter = null,
        [Description(
            "Optional filter: only events created at or before this instant. ISO 8601 (e.g. \"2025-01-31\" or \"2025-01-31T23:59:59Z\").")]
        string? createdBefore = null,
        [Description("Maximum events to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new GroupAuditEventListOptions
        {
            CreatedAfter = ParseInstant(createdAfter, nameof(createdAfter)),
            CreatedBefore = ParseInstant(createdBefore, nameof(createdBefore)),
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<AuditEventSummary> collected = [];
        var truncated = false;
        await foreach (var auditEvent in auditEvents.ListForGroupAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(auditEvent));
        }

        return GitLabContent.Wrap(new AuditEventListResult(collected, truncated), "groups/:id/audit_events");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_system_hooks
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_system_hooks", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every system hook registered on the instance -- instance-wide webhooks that fire on events across every project and group. Requires instance administrator access.")]
    public async Task<CallToolResult> ListSystemHooksAsync(
        [Description("Maximum system hooks to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<HookSummary> collected = [];
        var truncated = false;
        await foreach (var hook in systemHooks.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(hook));
        }

        return GitLabContent.Wrap(new SystemHookListResult(collected, truncated), "hooks");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_system_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_system_hook", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one system hook by id. Requires instance administrator access.")]
    public async Task<CallToolResult> GetSystemHookAsync(
        [Description("The system hook's numeric id, from gitlab_list_system_hooks or gitlab_create_system_hook.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        var hook = await systemHooks.GetAsync(hookId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "hooks/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_system_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_system_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a system hook's URL, description or event toggles; fields left unset keep their current value. URL variables and custom headers, when supplied, replace the hook's existing set wholesale rather than merging. Requires instance administrator access.")]
    public async Task<CallToolResult> UpdateSystemHookAsync(
        [Description("The system hook's numeric id, from gitlab_list_system_hooks or gitlab_create_system_hook.")]
        long hookId,
        [Description("The URL GitLab will POST event payloads to. Omit to leave unchanged.")]
        string? url = null,
        [Description("Display name for the hook. Omit to leave unchanged.")]
        string? name = null,
        [Description("Description. Omit to leave unchanged.")]
        string? description = null,
        [Description("Fire on push events across every project. Omit to leave unchanged.")]
        bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit to leave unchanged.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit to leave unchanged.")]
        string? branchFilterStrategy = null,
        [Description("Fire on tag push events across every project. Omit to leave unchanged.")]
        bool? tagPushEvents = null,
        [Description("Fire on merge request events across every project. Omit to leave unchanged.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire when any repository is updated. Omit to leave unchanged.")]
        bool? repositoryUpdateEvents = null,
        [Description(
            "Replacement secret token GitLab sends back as the X-Gitlab-Token header. Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? secretToken = null,
        [Description(
            "Replacement HMAC signing key (whsec_... form). Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit to leave unchanged.")]
        bool? enableSslVerification = null,
        [Description(
            "Replaces the hook's URL variables wholesale, as a JSON object of name/value string pairs, e.g. {\"ENV\":\"prod\"}. Omit to leave the existing set unchanged.")]
        string? urlVariablesJson = null,
        [Description(
            "Replaces the hook's custom request headers wholesale, as a JSON object of name/value string pairs. Omit to leave the existing set unchanged.")]
        string? customHeadersJson = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateSystemHookRequest
        {
            Url = ParseUriOptional(url, nameof(url)),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            TagPushEvents = tagPushEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            RepositoryUpdateEvents = repositoryUpdateEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification,
            UrlVariables = ParseUrlVariables(urlVariablesJson, nameof(urlVariablesJson)),
            CustomHeaders = ParseCustomHeaders(customHeadersJson, nameof(customHeadersJson))
        };

        var hook = await systemHooks.UpdateAsync(hookId, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "hooks/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_system_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_system_hook", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a system hook. Requires instance administrator access.")]
    public async Task<HookDeleteResult> DeleteSystemHookAsync(
        [Description("The system hook's numeric id.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        await systemHooks.DeleteAsync(hookId, cancellationToken);
        return new HookDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_test_system_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_test_system_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Fires a test run of a system hook using mock data, to verify the receiving endpoint is reachable and responds correctly. Requires instance administrator access.")]
    public async Task<HookTestResult> TestSystemHookAsync(
        [Description("The system hook's numeric id.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        await systemHooks.TestAsync(hookId, cancellationToken);
        return new HookTestResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_project_hooks
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_project_hooks", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every webhook configured on a project (bounded). Secret tokens and header/URL-variable values are never included, only whether one is set.")]
    public async Task<CallToolResult> ListProjectHooksAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum hooks to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<HookSummary> collected = [];
        var truncated = false;
        await foreach (var hook in projectHooks.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(hook));
        }

        return GitLabContent.Wrap(new ProjectHookListResult(collected, truncated), "projects/:id/hooks");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_project_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_project_hook", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one project webhook by id. Secret tokens and header/URL-variable values are never included, only whether one is set.")]
    public async Task<CallToolResult> GetProjectHookAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id, from gitlab_list_project_hooks or gitlab_create_project_hook.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        var hook = await projectHooks.GetAsync(project, hookId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "projects/:id/hooks/:hook_id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_project_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_project_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a project webhook's URL, description or event toggles; fields left unset keep their current value. URL variables and custom headers, when supplied, replace the hook's existing set wholesale rather than merging.")]
    public async Task<CallToolResult> UpdateProjectHookAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id, from gitlab_list_project_hooks or gitlab_create_project_hook.")]
        long hookId,
        [Description("The URL GitLab will POST event payloads to. Omit to leave unchanged.")]
        string? url = null,
        [Description("Display name for the webhook. Omit to leave unchanged.")]
        string? name = null,
        [Description("Description. Omit to leave unchanged.")]
        string? description = null,
        [Description("Fire on push events. Omit to leave unchanged.")]
        bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit to leave unchanged.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit to leave unchanged.")]
        string? branchFilterStrategy = null,
        [Description("Fire on issue events. Omit to leave unchanged.")]
        bool? issuesEvents = null,
        [Description("Fire on confidential issue events. Omit to leave unchanged.")]
        bool? confidentialIssuesEvents = null,
        [Description("Fire on merge request events. Omit to leave unchanged.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire on tag push events. Omit to leave unchanged.")]
        bool? tagPushEvents = null,
        [Description("Fire on comment (note) events. Omit to leave unchanged.")]
        bool? noteEvents = null,
        [Description("Fire on confidential comment (note) events. Omit to leave unchanged.")]
        bool? confidentialNoteEvents = null,
        [Description("Fire on CI/CD job events. Omit to leave unchanged.")]
        bool? jobEvents = null,
        [Description("Fire on CI/CD pipeline events. Omit to leave unchanged.")]
        bool? pipelineEvents = null,
        [Description("Fire on wiki page events. Omit to leave unchanged.")]
        bool? wikiPageEvents = null,
        [Description("Fire on deployment events. Omit to leave unchanged.")]
        bool? deploymentEvents = null,
        [Description("Fire on release events. Omit to leave unchanged.")]
        bool? releasesEvents = null,
        [Description(
            "Replacement secret token GitLab sends back as the X-Gitlab-Token header. Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? secretToken = null,
        [Description(
            "Replacement HMAC signing key (whsec_... form). Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit to leave unchanged.")]
        bool? enableSslVerification = null,
        [Description(
            "Replaces the hook's URL variables wholesale, as a JSON object of name/value string pairs, e.g. {\"ENV\":\"prod\"}. To change one variable without resending the others, use GitLab's dedicated per-variable endpoint instead (not exposed by this tool). Omit to leave the existing set unchanged.")]
        string? urlVariablesJson = null,
        [Description(
            "Replaces the hook's custom request headers wholesale, as a JSON object of name/value string pairs. Omit to leave the existing set unchanged.")]
        string? customHeadersJson = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProjectHookRequest
        {
            Url = ParseUriOptional(url, nameof(url)),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            IssuesEvents = issuesEvents,
            ConfidentialIssuesEvents = confidentialIssuesEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            TagPushEvents = tagPushEvents,
            NoteEvents = noteEvents,
            ConfidentialNoteEvents = confidentialNoteEvents,
            JobEvents = jobEvents,
            PipelineEvents = pipelineEvents,
            WikiPageEvents = wikiPageEvents,
            DeploymentEvents = deploymentEvents,
            ReleasesEvents = releasesEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification,
            UrlVariables = ParseUrlVariables(urlVariablesJson, nameof(urlVariablesJson)),
            CustomHeaders = ParseCustomHeaders(customHeadersJson, nameof(customHeadersJson))
        };

        var hook = await projectHooks.UpdateAsync(project, hookId, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "projects/:id/hooks/:hook_id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_project_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_project_hook", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a project webhook.")]
    public async Task<HookDeleteResult> DeleteProjectHookAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        await projectHooks.DeleteAsync(project, hookId, cancellationToken);
        return new HookDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_test_project_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_test_project_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Fires a test delivery of one event type against a project webhook, using mock data. GitLab rate-limits this to 5 tests per minute per user per project.")]
    public async Task<HookTestResult> TestProjectHookAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id.")]
        long hookId,
        [Description(
            "The event type to simulate: \"push_events\", \"issues_events\", \"confidential_issues_events\", \"merge_requests_events\", \"note_events\", \"confidential_note_events\", \"job_events\", \"pipeline_events\", \"deployment_events\", \"feature_flag_events\", \"milestone_events\", or \"emoji_events\".")]
        string triggerEvent,
        CancellationToken cancellationToken = default)
    {
        var trigger = ParseWebhookTestTrigger(triggerEvent, nameof(triggerEvent));
        await projectHooks.TestAsync(project, hookId, trigger, cancellationToken);
        return new HookTestResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_resend_project_hook_delivery
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_resend_project_hook_delivery", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Replays one previously logged project webhook delivery, resending the same payload to the same URL.")]
    public async Task<HookDeliveryResendResult> ResendProjectHookDeliveryAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The webhook's numeric id.")]
        long hookId,
        [Description("The delivery's numeric id, from gitlab_list_project_hook_deliveries.")]
        long deliveryId,
        CancellationToken cancellationToken = default)
    {
        await projectHooks.ResendEventAsync(project, hookId, deliveryId, cancellationToken);
        return new HookDeliveryResendResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_group_hooks
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_group_hooks", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every webhook configured on a group (bounded). Secret tokens and header/URL-variable values are never included, only whether one is set.")]
    public async Task<CallToolResult> ListGroupHooksAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("Maximum hooks to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<HookSummary> collected = [];
        var truncated = false;
        await foreach (var hook in groupHooks.ListAsync(group, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(hook));
        }

        return GitLabContent.Wrap(new GroupHookListResult(collected, truncated), "groups/:id/hooks");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_group_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_group_hook", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one group webhook by id. Secret tokens and header/URL-variable values are never included, only whether one is set.")]
    public async Task<CallToolResult> GetGroupHookAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id, from gitlab_list_group_hooks or gitlab_create_group_hook.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        var hook = await groupHooks.GetAsync(group, hookId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "groups/:id/hooks/:hook_id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_group_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_group_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a group webhook's URL, description or event toggles; fields left unset keep their current value. URL variables and custom headers, when supplied, replace the hook's existing set wholesale rather than merging.")]
    public async Task<CallToolResult> UpdateGroupHookAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id, from gitlab_list_group_hooks or gitlab_create_group_hook.")]
        long hookId,
        [Description("The URL GitLab will POST event payloads to. Omit to leave unchanged.")]
        string? url = null,
        [Description("Display name for the webhook. Omit to leave unchanged.")]
        string? name = null,
        [Description("Description. Omit to leave unchanged.")]
        string? description = null,
        [Description("Fire on push events. Omit to leave unchanged.")]
        bool? pushEvents = null,
        [Description(
            "Restrict push events to branches matching this filter (wildcard or regex, per branchFilterStrategy). Omit to leave unchanged.")]
        string? pushEventsBranchFilter = null,
        [Description(
            "How pushEventsBranchFilter is interpreted: \"wildcard\", \"regex\", or \"all_branches\". Omit to leave unchanged.")]
        string? branchFilterStrategy = null,
        [Description("Fire on issue events. Omit to leave unchanged.")]
        bool? issuesEvents = null,
        [Description("Fire on confidential issue events. Omit to leave unchanged.")]
        bool? confidentialIssuesEvents = null,
        [Description("Fire on merge request events. Omit to leave unchanged.")]
        bool? mergeRequestsEvents = null,
        [Description("Fire on tag push events. Omit to leave unchanged.")]
        bool? tagPushEvents = null,
        [Description("Fire on comment (note) events. Omit to leave unchanged.")]
        bool? noteEvents = null,
        [Description("Fire on confidential comment (note) events. Omit to leave unchanged.")]
        bool? confidentialNoteEvents = null,
        [Description("Fire on CI/CD job events. Omit to leave unchanged.")]
        bool? jobEvents = null,
        [Description("Fire on CI/CD pipeline events. Omit to leave unchanged.")]
        bool? pipelineEvents = null,
        [Description("Fire on wiki page events. Omit to leave unchanged.")]
        bool? wikiPageEvents = null,
        [Description("Fire on deployment events. Omit to leave unchanged.")]
        bool? deploymentEvents = null,
        [Description("Fire on release events. Omit to leave unchanged.")]
        bool? releasesEvents = null,
        [Description("Fire when a subgroup is created or removed under this group. Omit to leave unchanged.")]
        bool? subgroupEvents = null,
        [Description("Fire when a project is created or removed under this group. Omit to leave unchanged.")]
        bool? projectEvents = null,
        [Description("Fire on group membership changes. Omit to leave unchanged.")]
        bool? memberEvents = null,
        [Description(
            "Replacement secret token GitLab sends back as the X-Gitlab-Token header. Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? secretToken = null,
        [Description(
            "Replacement HMAC signing key (whsec_... form). Write-only: never returned by any tool. Omit to leave unchanged.")]
        string? signingToken = null,
        [Description("Whether GitLab verifies the receiver's SSL certificate. Omit to leave unchanged.")]
        bool? enableSslVerification = null,
        [Description(
            "Replaces the hook's URL variables wholesale, as a JSON object of name/value string pairs, e.g. {\"ENV\":\"prod\"}. To change one variable without resending the others, use GitLab's dedicated per-variable endpoint instead (not exposed by this tool). Omit to leave the existing set unchanged.")]
        string? urlVariablesJson = null,
        [Description(
            "Replaces the hook's custom request headers wholesale, as a JSON object of name/value string pairs. Omit to leave the existing set unchanged.")]
        string? customHeadersJson = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateGroupHookRequest
        {
            Url = ParseUriOptional(url, nameof(url)),
            Name = name,
            Description = description,
            PushEvents = pushEvents,
            PushEventsBranchFilter = pushEventsBranchFilter,
            BranchFilterStrategy = ParseBranchFilterStrategy(branchFilterStrategy),
            IssuesEvents = issuesEvents,
            ConfidentialIssuesEvents = confidentialIssuesEvents,
            MergeRequestsEvents = mergeRequestsEvents,
            TagPushEvents = tagPushEvents,
            NoteEvents = noteEvents,
            ConfidentialNoteEvents = confidentialNoteEvents,
            JobEvents = jobEvents,
            PipelineEvents = pipelineEvents,
            WikiPageEvents = wikiPageEvents,
            DeploymentEvents = deploymentEvents,
            ReleasesEvents = releasesEvents,
            SubgroupEvents = subgroupEvents,
            ProjectEvents = projectEvents,
            MemberEvents = memberEvents,
            Token = secretToken,
            SigningToken = signingToken,
            EnableSslVerification = enableSslVerification,
            UrlVariables = ParseUrlVariables(urlVariablesJson, nameof(urlVariablesJson)),
            CustomHeaders = ParseCustomHeaders(customHeadersJson, nameof(customHeadersJson))
        };

        var hook = await groupHooks.UpdateAsync(group, hookId, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(hook), "groups/:id/hooks/:hook_id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_group_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_group_hook", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a group webhook.")]
    public async Task<HookDeleteResult> DeleteGroupHookAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id.")]
        long hookId,
        CancellationToken cancellationToken = default)
    {
        await groupHooks.DeleteAsync(group, hookId, cancellationToken);
        return new HookDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_test_group_hook
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_test_group_hook", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Fires a test delivery of one event type against a group webhook, using mock data. GitLab rate-limits this to 5 tests per minute per user per group.")]
    public async Task<HookTestResult> TestGroupHookAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id.")]
        long hookId,
        [Description(
            "The event type to simulate: \"push_events\", \"issues_events\", \"confidential_issues_events\", \"merge_requests_events\", \"note_events\", \"confidential_note_events\", \"job_events\", \"pipeline_events\", \"deployment_events\", \"feature_flag_events\", \"milestone_events\", or \"emoji_events\".")]
        string triggerEvent,
        CancellationToken cancellationToken = default)
    {
        var trigger = ParseWebhookTestTrigger(triggerEvent, nameof(triggerEvent));
        await groupHooks.TestAsync(group, hookId, trigger, cancellationToken);
        return new HookTestResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_resend_group_hook_delivery
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_resend_group_hook_delivery", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Replays one previously logged group webhook delivery, resending the same payload to the same URL.")]
    public async Task<HookDeliveryResendResult> ResendGroupHookDeliveryAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The webhook's numeric id.")]
        long hookId,
        [Description("The delivery's numeric id, from gitlab_list_group_hook_deliveries.")]
        long deliveryId,
        CancellationToken cancellationToken = default)
    {
        await groupHooks.ResendEventAsync(group, hookId, deliveryId, cancellationToken);
        return new HookDeliveryResendResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_project_integrations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_project_integrations", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists a project's active third-party integrations (Slack, Jira, Datadog, etc.), bounded. Integration-specific settings values are never included, only identifying metadata.")]
    public async Task<CallToolResult> ListProjectIntegrationsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Maximum integrations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<IntegrationSummary> collected = [];
        var truncated = false;
        await foreach (var integration in integrations.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(integration));
        }

        return GitLabContent.Wrap(new IntegrationListResult(collected, truncated), "projects/:id/integrations");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_project_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_project_integration", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one project integration by slug (e.g. \"jira\", \"slack\", \"datadog\"). Settings values (API keys, webhook URLs, passwords) are write-only and never returned by any tool.")]
    public async Task<CallToolResult> GetProjectIntegrationAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\". Matches the identifier GitLab's own integrations settings page uses in its URL.")]
        string slug,
        CancellationToken cancellationToken = default)
    {
        var integration = await integrations.GetAsync(project, slug, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(integration), "projects/:id/integrations/:slug");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_group_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_group_integration", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one group integration by slug (e.g. \"jira\", \"slack\", \"datadog\"). Settings values (API keys, webhook URLs, passwords) are write-only and never returned by any tool.")]
    public async Task<CallToolResult> GetGroupIntegrationAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\".")]
        string slug,
        CancellationToken cancellationToken = default)
    {
        var integration = await integrations.GetForGroupAsync(group, slug, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(integration), "groups/:id/integrations/:slug");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_project_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_project_integration", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Creates or fully replaces a project integration's settings by slug (e.g. \"jira\", \"slack\", \"datadog\"), covering any of GitLab's roughly 50 integration types through one generic settings-dictionary call. This is a full replace, not a merge: any setting the integration supports but settingsJson omits is cleared to its default -- never read-then-write-back without re-supplying every credential field, since GitLab masks credentials out of what it returns. Settings values (API keys, webhook URLs, passwords) are write-only and never returned by any tool.")]
    public async Task<CallToolResult> SetProjectIntegrationAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\". Matches the identifier GitLab's own integrations settings page uses in its URL.")]
        string slug,
        [Description(
            "The integration's settings as a JSON object string, e.g. {\"webhook\":\"https://hooks.example.com/...\",\"notify_only_broken_pipelines\":true}. Field names match GitLab's REST API for that integration.")]
        string settingsJson,
        CancellationToken cancellationToken = default)
    {
        var settings = ParseSettingsObject(settingsJson, nameof(settingsJson));
        var integration = await integrations.SetAsync(project, slug, settings, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(integration), "projects/:id/:slug (set)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_disable_project_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_disable_project_integration", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Removes a project's configuration for one integration by slug, discarding its settings. Re-enabling means configuring the integration again with gitlab_set_project_integration.")]
    public async Task<IntegrationDisableResult> DisableProjectIntegrationAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\".")]
        string slug,
        CancellationToken cancellationToken = default)
    {
        await integrations.DisableAsync(project, slug, cancellationToken);
        return new IntegrationDisableResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_disable_group_integration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_disable_group_integration", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Removes a group's configuration for one integration by slug, discarding its settings. Re-enabling means configuring the integration again with gitlab_set_group_integration.")]
    public async Task<IntegrationDisableResult> DisableGroupIntegrationAsync(
        [Description("Group: numeric id or \"namespace/path\".")]
        string group,
        [Description("The integration's slug, e.g. \"slack\", \"jira\", \"datadog\", \"mattermost\".")]
        string slug,
        CancellationToken cancellationToken = default)
    {
        await integrations.DisableForGroupAsync(group, slug, cancellationToken);
        return new IntegrationDisableResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_google_cloud_setup_script
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_google_cloud_setup_script", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Gets, as plain shell script text, either the script that sets up a project's Google Cloud integration (optionally also enabling Artifact Registry), or -- when runnerProvisioningGoogleCloudProjectId is supplied -- the different script that configures that Google Cloud project for GitLab runner provisioning instead. Experimental GitLab surface: exact script content may change without notice.")]
    public async Task<CallToolResult> GetGoogleCloudSetupScriptAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description(
            "When set, switches this tool to the runner-provisioning script and names the Google Cloud project the runners should be provisioned into. Mutually exclusive with enableArtifactRegistry/artifactRegistryGoogleCloudProjectId.")]
        string? runnerProvisioningGoogleCloudProjectId = null,
        [Description(
            "Only used for the integration-setup script (i.e. when runnerProvisioningGoogleCloudProjectId is omitted): whether the generated script should also enable the Google Artifact Management integration. Default false.")]
        bool enableArtifactRegistry = false,
        [Description(
            "Only used together with enableArtifactRegistry: the Google Cloud project ID the Artifact Registry integration should point at. Required when enableArtifactRegistry is true.")]
        string? artifactRegistryGoogleCloudProjectId = null,
        CancellationToken cancellationToken = default)
    {
        GitLabFileResponse file;
        string source;

        if (runnerProvisioningGoogleCloudProjectId is not null)
        {
            if (enableArtifactRegistry || artifactRegistryGoogleCloudProjectId is not null)
                throw new McpException(
                    "runnerProvisioningGoogleCloudProjectId cannot be combined with enableArtifactRegistry or artifactRegistryGoogleCloudProjectId.");

            file = await platformIntegrations.GetGoogleCloudRunnerDeploymentSetupScriptAsync(project,
                runnerProvisioningGoogleCloudProjectId, cancellationToken);
            source = "projects/:id/google_cloud/setup/runner_deployment_project.sh";
        }
        else
        {
            if (enableArtifactRegistry && artifactRegistryGoogleCloudProjectId is null)
                throw new McpException(
                    "artifactRegistryGoogleCloudProjectId is required when enableArtifactRegistry is true.");

            var options = new GoogleCloudIntegrationSetupScriptOptions
            {
                EnableGoogleCloudArtifactRegistry = enableArtifactRegistry ? true : null,
                GoogleCloudArtifactRegistryProjectId = artifactRegistryGoogleCloudProjectId
            };

            file = await platformIntegrations.GetGoogleCloudIntegrationSetupScriptAsync(project, options,
                cancellationToken);
            source = "projects/:id/google_cloud/setup/integrations.sh";
        }

        // GitLabFileResponse (not its Content stream alone) is what owns disposal of the body, the
        // underlying HTTP response and the per-operation cancellation source -- see its XML doc.
        await using var _ = file;
        using var reader = new StreamReader(file.Content);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var truncated = text.Length > MaxScriptChars;
        var body = truncated
            ? $"[script truncated at {MaxScriptChars} characters]{Environment.NewLine}{text[..MaxScriptChars]}"
            : text;

        return GitLabContent.WrapText(body, source);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_exchange_platform_token
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_exchange_platform_token", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Issues a short-lived JWT scoped to one modular-service audience (currently only GitLab's own Artifact Registry). The token is a bearer credential for that service: it is returned once, in this response, and never logged or echoed by any other tool. Experimental GitLab surface.")]
    public async Task<CallToolResult> ExchangePlatformTokenAsync(
        [Description(
            "The audience to scope the token to. Currently the only value GitLab accepts is \"artifact_registry\" (GitLab's Artifact Registry).")]
        string audience,
        [Description("Requested token lifetime in seconds, 1-43200. Omit for GitLab's default (300).")]
        int? expiresIn = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TokenExchangeRequest
            { Audience = ParseTokenExchangeAudience(audience, nameof(audience)), ExpiresIn = expiresIn };
        var result = await platformIntegrations.ExchangeTokenAsync(request, cancellationToken);
        var token = result.Token ?? throw new McpException("GitLab returned no token from the token exchange.");
        return GitLabContent.Wrap(new PlatformTokenExchangeResult(token), "token_exchange");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_ci_job_allowed_agents
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_ci_job_allowed_agents", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the GitLab agents for Kubernetes available to the calling CI/CD job token. Only meaningful when this server is authenticated with a job token, not a personal access token -- with any other token type the response is unrelated or empty. GitLab's own OpenAPI spec documents the response as the CI job entity the token belongs to rather than an agent list, so this tool returns that job's identifying fields; treat the result as identifying the job, not as a literal agent list.")]
    public async Task<CallToolResult> GetCiJobAllowedAgentsAsync(CancellationToken cancellationToken)
    {
        var job = await platformIntegrations.GetAllowedAgentsAsync(cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToAllowedAgentsSummary(job), "job/allowed_agents");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_instance_features
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_instance_features", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every instance-wide GitLab development flag (Flipper flag) that currently has a gate set, together with its overall state and individual gates. These are GitLab's own development flags, not a project's feature flags -- use gitlab_list_feature_flags for those. Requires instance administrator access.")]
    public async Task<CallToolResult> ListInstanceFeaturesAsync(
        [Description("Maximum features to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<InstanceFeatureSummary> collected = [];
        var truncated = false;
        await foreach (var feature in features.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(feature));
        }

        return GitLabContent.Wrap(new InstanceFeatureListResult(collected, truncated), "features");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_feature_definitions
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_feature_definitions", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists the catalogue of instance feature flag definitions GitLab ships with itself: owning team, type, milestone introduced, and rollout-tracking issue. This is the definition catalogue, not current on/off state -- use gitlab_list_instance_features for that. Requires instance administrator access.")]
    public async Task<CallToolResult> ListFeatureDefinitionsAsync(
        [Description("Maximum definitions to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<InstanceFeatureDefinitionSummary> collected = [];
        var truncated = false;
        await foreach (var definition in features.ListDefinitionsAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(definition));
        }

        return GitLabContent.Wrap(new InstanceFeatureDefinitionListResult(collected, truncated),
            "features/definitions");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_instance_feature
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_instance_feature", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Enables, disables, or sets a percentage rollout for an instance-wide GitLab development flag, creating it if it does not exist. Requires instance administrator access.")]
    public async Task<CallToolResult> SetInstanceFeatureAsync(
        [Description("The feature's name, e.g. \"my_flag\".")]
        string name,
        [Description(
            "The gate to set: \"enable\" (fully on), \"disable\" (fully off), or \"percentage\" (a percentage rollout -- requires percentage).")]
        string mode,
        [Description("Required when mode is \"percentage\": the rollout percentage, 0-100.")]
        int? percentage = null,
        [Description(
            "Only used when mode is \"percentage\": which percentage gate this sets -- \"percentage_of_actors\" or \"percentage_of_time\". Omit for GitLab's default (percentage_of_time).")]
        string? percentageKey = null,
        [Description(
            "Scopes the gate to one Flipper feature group name instead of everyone. Only valid when mode is not \"percentage\"; at most one scoping parameter (featureGroup/user/group/namespace/project/organization/repository/runner/endpoint) may be set.")]
        string? featureGroup = null,
        [Description(
            "Scopes the gate to one username, or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? user = null,
        [Description(
            "Scopes the gate to one group path (e.g. \"gitlab-org\"), or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? group = null,
        [Description(
            "Scopes the gate to one user or group namespace path, or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? @namespace = null,
        [Description(
            "Scopes the gate to one project path (e.g. \"gitlab-org/gitlab\"), or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? project = null,
        [Description(
            "Scopes the gate to one organization id or path, or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? organization = null,
        [Description(
            "Scopes the gate to one repository path (e.g. \"gitlab-org/gitlab.git\"), or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? repository = null,
        [Description(
            "Scopes the gate to one runner id, or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? runner = null,
        [Description(
            "Scopes the gate to one API caller id (e.g. \"GET /api/v4/projects/:id\"), or several comma-separated. Only valid when mode is not \"percentage\"; at most one scoping parameter may be set.")]
        string? endpoint = null,
        [Description(
            "Skips GitLab's own validation, such as the check that a YAML definition exists for this flag name. Default false.")]
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var isPercentage = string.Equals(mode, "percentage", StringComparison.OrdinalIgnoreCase);
        string?[] actors = [featureGroup, user, group, @namespace, project, organization, repository, runner, endpoint];
        var actorCount = actors.Count(static a => a is not null);

        var request = mode.ToLowerInvariant() switch
        {
            "enable" => SetFeatureRequest.Enable(),
            "disable" => SetFeatureRequest.Disable(),
            "percentage" => SetFeatureRequest.ForPercentage(percentage ??
                                                            throw new McpException(
                                                                "percentage is required when mode is \"percentage\".")),
            _ => throw new McpException("mode must be \"enable\", \"disable\", or \"percentage\".")
        };

        if (isPercentage)
        {
            if (actorCount > 0)
                throw new McpException("Scoping parameters cannot be combined with mode \"percentage\".");

            request = request with { Key = ParsePercentageKey(percentageKey) };
        }
        else
        {
            if (percentage is not null || percentageKey is not null)
                throw new McpException("percentage and percentageKey only apply when mode is \"percentage\".");

            if (actorCount > 1)
                throw new McpException(
                    "At most one scoping parameter (featureGroup/user/group/namespace/project/organization/repository/runner/endpoint) may be set.");

            request = request with
            {
                FeatureGroup = featureGroup,
                User = user,
                Group = group,
                Namespace = @namespace,
                Project = project,
                Organization = organization,
                Repository = repository,
                Runner = runner,
                Endpoint = endpoint
            };
        }

        request = request with { Force = force ? true : null };

        var feature = await features.SetAsync(name, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(feature), "features/:name (set)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_instance_feature_gate
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_instance_feature_gate", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Deletes an instance-wide feature's gate, reverting it to its default (usually off) state. GitLab answers the same way whether or not the gate existed, so this never reports a missing feature. Requires instance administrator access.")]
    public async Task<InstanceFeatureGateDeleteResult> DeleteInstanceFeatureGateAsync(
        [Description("The feature's name.")] string name,
        CancellationToken cancellationToken = default)
    {
        await features.DeleteAsync(name, cancellationToken);
        return new InstanceFeatureGateDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_feature_flags
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_feature_flags", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Lists a project's feature flags, newest first (bounded).")]
    public async Task<CallToolResult> ListFeatureFlagsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Optional filter by state: \"enabled\" or \"disabled\". Omit for all.")]
        string? scope = null,
        [Description("Maximum feature flags to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new FeatureFlagListOptions
            { Scope = ParseFeatureFlagState(scope), PerPage = Math.Min(limit + 1, MaxLimit) };

        List<FeatureFlagSummary> collected = [];
        var truncated = false;
        await foreach (var flag in featureFlags.ListAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(flag));
        }

        return GitLabContent.Wrap(new FeatureFlagListResult(collected, truncated), "projects/:id/feature_flags");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_feature_flag
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_feature_flag", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one project feature flag by name.")]
    public async Task<CallToolResult> GetFeatureFlagAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The flag's name.")] string name,
        CancellationToken cancellationToken = default)
    {
        var flag = await featureFlags.GetAsync(project, name, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(flag), "projects/:id/feature_flags/:name");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_feature_flag
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_feature_flag", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Renames, toggles, or edits the strategies of a project feature flag; fields left unset (omitted) keep their current value. The route addresses the flag by its current name even when this call renames it. strategiesJson, when supplied, fully replaces the flag's strategy list: each array element is {\"id\":<existing strategy id>,\"name\":<strategy type, e.g. \"default\" or \"gradualRolloutUserId\">,\"parameters\":{...},\"scopes\":[{\"environmentScope\":\"*\"}],\"userListId\":<id>,\"destroy\":true} -- include \"id\" to edit an existing strategy, omit it to add a new one, and add \"destroy\":true alongside \"id\" to remove one.")]
    public async Task<CallToolResult> UpdateFeatureFlagAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The flag's current name.")]
        string name,
        [Description("New name for the flag. Omit to leave unchanged.")]
        string? newName = null,
        [Description("New description. Omit to leave unchanged.")]
        string? description = null,
        [Description("Whether the flag is active. Omit to leave unchanged.")]
        bool? active = null,
        [Description(
            "Full replacement strategy list as a JSON array (see tool description). Omit to leave the existing strategies untouched.")]
        string? strategiesJson = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateFeatureFlagRequest
        {
            Name = newName,
            Description = description,
            Active = active,
            Strategies = ParseStrategies(strategiesJson, nameof(strategiesJson))
        };

        var flag = await featureFlags.UpdateAsync(project, name, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(flag), "projects/:id/feature_flags/:name (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_feature_flag
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_feature_flag", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes a project feature flag and returns the flag as it was just before deletion.")]
    public async Task<CallToolResult> DeleteFeatureFlagAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The flag's name.")] string name,
        CancellationToken cancellationToken = default)
    {
        var flag = await featureFlags.DeleteAsync(project, name, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(flag), "projects/:id/feature_flags/:name (delete)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_feature_flag_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_feature_flag_settings", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets the minimum GitLab role required to change a project's feature flags.")]
    public async Task<CallToolResult> GetFeatureFlagSettingsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        CancellationToken cancellationToken = default)
    {
        var settings = await featureFlags.GetSettingsAsync(project, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(settings), "projects/:id/feature_flags_client (settings)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_feature_flag_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_feature_flag_settings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Sets the minimum GitLab role required to change a project's feature flags. Raising it above the caller's own role locks the caller out of further edits to feature flags on this project.")]
    public async Task<CallToolResult> UpdateFeatureFlagSettingsAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The new minimum role: \"no_one_allowed\", \"developer\", \"maintainer\", or \"owner\".")]
        string minimumRole,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateFeatureFlagSettingsRequest { MinimumRole = ParseMinimumRole(minimumRole) };
        var settings = await featureFlags.UpdateSettingsAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(settings),
            "projects/:id/feature_flags_client (settings update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_feature_flag_user_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_feature_flag_user_list", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one feature flag user list by its iid.")]
    public async Task<CallToolResult> GetFeatureFlagUserListAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The user list's iid (internal id), from gitlab_list_feature_flag_user_lists.")]
        long iid,
        CancellationToken cancellationToken = default)
    {
        var userList = await featureFlags.GetUserListAsync(project, iid, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(userList), "projects/:id/feature_flags_user_lists/:iid");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_feature_flag_user_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_feature_flag_user_list", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a feature flag user list from a comma-separated set of external user IDs, for percentage/user-targeted rollouts. Attach it to a strategy afterward via gitlab_update_feature_flag (strategy name \"gitlabUserList\", with userListId set to this list's id).")]
    public async Task<CallToolResult> CreateFeatureFlagUserListAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("Display name for the user list.")]
        string name,
        [Description("Comma-separated external user IDs to include, e.g. \"user1,user2,user3\".")]
        string userXids,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateFeatureFlagUserListRequest { Name = name, UserXids = userXids };
        var userList = await featureFlags.CreateUserListAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(userList), "projects/:id/feature_flags_user_lists (create)");
    }

    // ---- backlog-split/admin/part-4.json below ----

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_feature_flag_user_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_feature_flag_user_list", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Renames a feature flag user list, or wholesale-replaces its member user IDs. userXids, when supplied, fully replaces the list's membership -- it is not an append. Fields left unset (omitted) keep their current value.")]
    public async Task<CallToolResult> UpdateFeatureFlagUserListAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The user list's iid (internal id), from gitlab_list_feature_flag_user_lists.")]
        long iid,
        [Description("New display name for the list. Omit to leave unchanged.")]
        string? name = null,
        [Description(
            "Full replacement set of comma-separated external user IDs, e.g. \"user1,user2,user3\". This replaces the existing membership wholesale, not an append. Omit to leave unchanged.")]
        string? userXids = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateFeatureFlagUserListRequest { Name = name, UserXids = userXids };
        var userList = await featureFlags.UpdateUserListAsync(project, iid, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(userList),
            "projects/:id/feature_flags_user_lists/:iid (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_feature_flag_user_list
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_feature_flag_user_list", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Deletes a feature flag user list. GitLab refuses with a conflict while a strategy on one of this project's feature flags still references it.")]
    public async Task<FeatureFlagUserListDeleteResult> DeleteFeatureFlagUserListAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The user list's iid (internal id), from gitlab_list_feature_flag_user_lists.")]
        long iid,
        CancellationToken cancellationToken = default)
    {
        await featureFlags.DeleteUserListAsync(project, iid, cancellationToken);
        return new FeatureFlagUserListDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_oauth_applications
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_oauth_applications", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every instance-wide OAuth application registered on the GitLab instance (bounded). Client secrets are never included -- only gitlab_create_oauth_application's response ever discloses one. Requires instance administrator access.")]
    public async Task<CallToolResult> ListOAuthApplicationsAsync(
        [Description("Maximum applications to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<OAuthApplicationSummary> collected = [];
        var truncated = false;
        await foreach (var app in applications.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(app));
        }

        return GitLabContent.Wrap(new OAuthApplicationListResult(collected, truncated), "applications");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_oauth_application
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_oauth_application", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Registers a new instance-wide OAuth application. The response's plaintext client secret is shown exactly once, in this response, and is never retrievable again or echoed by any other tool -- persist it immediately. Requires instance administrator access.")]
    public async Task<CallToolResult> CreateOAuthApplicationAsync(
        [Description("Display name for the application.")]
        string name,
        [Description(
            "The OAuth redirect (callback) URI clients are sent back to after authorizing. GitLab accepts multiple URIs here, one per line.")]
        string redirectUri,
        [Description("Space-separated OAuth scopes to grant, e.g. \"api read_user\".")]
        string scopes,
        [Description(
            "Whether the application is confidential (can securely hold a client secret) rather than public. Omit for GitLab's default.")]
        bool? confidential = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateApplicationRequest
            { Name = name, RedirectUri = redirectUri, Scopes = scopes, Confidential = confidential };
        var app = await applications.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(app), "applications (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_oauth_application
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_oauth_application", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Deletes an instance-wide OAuth application. Requires instance administrator access.")]
    public async Task<OAuthApplicationDeleteResult> DeleteOAuthApplicationAsync(
        [Description(
            "The application's numeric id, from gitlab_list_oauth_applications or gitlab_create_oauth_application.")]
        long applicationId,
        CancellationToken cancellationToken = default)
    {
        await applications.DeleteAsync(applicationId, cancellationToken);
        return new OAuthApplicationDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_my_oauth_applications
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_my_oauth_applications", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists OAuth applications the current user -- the identity this server's configured token belongs to -- has registered for themselves (bounded). Client secrets are never included -- only gitlab_create_my_oauth_application's response ever discloses one.")]
    public async Task<CallToolResult> ListMyOAuthApplicationsAsync(
        [Description("Maximum applications to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        List<OAuthApplicationSummary> collected = [];
        var truncated = false;
        await foreach (var app in applications.ListForCurrentUserAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(app));
        }

        return GitLabContent.Wrap(new OAuthApplicationListResult(collected, truncated), "user/applications");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_my_oauth_application
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_my_oauth_application", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Registers a personal OAuth application under the current user's own account, e.g. for a CI integration the caller owns. The response's plaintext client secret is shown exactly once, in this response, and is never retrievable again or echoed by any other tool -- persist it immediately.")]
    public async Task<CallToolResult> CreateMyOAuthApplicationAsync(
        [Description("Display name for the application.")]
        string name,
        [Description(
            "The OAuth redirect (callback) URI clients are sent back to after authorizing. GitLab accepts multiple URIs here, one per line.")]
        string redirectUri,
        [Description("Space-separated OAuth scopes to grant, e.g. \"api read_user\".")]
        string scopes,
        [Description(
            "Whether the application is confidential (can securely hold a client secret) rather than public. Omit for GitLab's default.")]
        bool? confidential = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateApplicationRequest
            { Name = name, RedirectUri = redirectUri, Scopes = scopes, Confidential = confidential };
        var app = await applications.CreateForCurrentUserAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(app), "user/applications (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_my_oauth_application
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_my_oauth_application", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Deletes one of the current user's own OAuth applications.")]
    public async Task<OAuthApplicationDeleteResult> DeleteMyOAuthApplicationAsync(
        [Description(
            "The application's numeric id, from gitlab_list_my_oauth_applications or gitlab_create_my_oauth_application.")]
        long applicationId,
        CancellationToken cancellationToken = default)
    {
        await applications.DeleteForCurrentUserAsync(applicationId, cancellationToken);
        return new OAuthApplicationDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_broadcast_messages
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_broadcast_messages", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every broadcast message configured on the instance, active or not (bounded). Requires instance administrator access.")]
    public async Task<CallToolResult> ListBroadcastMessagesAsync(
        [Description("Maximum messages to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new BroadcastMessageListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<BroadcastMessageSummary> collected = [];
        var truncated = false;
        await foreach (var message in broadcastMessages.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(message));
        }

        return GitLabContent.Wrap(new BroadcastMessageListResult(collected, truncated), "broadcast_messages");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_broadcast_message
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_broadcast_message", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one broadcast message by id. Requires instance administrator access.")]
    public async Task<CallToolResult> GetBroadcastMessageAsync(
        [Description("The broadcast message's numeric id, from gitlab_list_broadcast_messages.")]
        long broadcastMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = await broadcastMessages.GetAsync(broadcastMessageId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(message), "broadcast_messages/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_create_broadcast_message
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_create_broadcast_message", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates an instance-wide broadcast banner or notification, with an optional schedule and target audience. Requires instance administrator access.")]
    public async Task<CallToolResult> CreateBroadcastMessageAsync(
        [Description("The message body shown to users. Supports GitLab Flavored Markdown.")]
        string message,
        [Description(
            "When the message starts being shown. ISO 8601 (e.g. \"2025-01-01T00:00:00Z\"). Omit for GitLab's default (now).")]
        string? startsAt = null,
        [Description("When the message stops being shown. ISO 8601. Omit for GitLab's default (one hour after start).")]
        string? endsAt = null,
        [Description("Background color as a CSS hex value, e.g. \"#E75E40\". Omit for GitLab's default.")]
        string? color = null,
        [Description("Font color as a CSS hex value, e.g. \"#FFFFFF\". Omit for GitLab's default.")]
        string? font = null,
        [Description(
            "Restricts the message to users whose role is in this comma-separated set of access level integers (10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner), e.g. \"30,40\". Omit to target every role.")]
        string? targetAccessLevels = null,
        [Description(
            "Restricts the message to pages whose path matches this glob, e.g. \"*/welcome\". Omit to target every page.")]
        string? targetPath = null,
        [Description(
            "Presentation style: \"banner\" (a strip across the page) or \"notification\" (a popup). Omit for GitLab's default (banner).")]
        string? broadcastType = null,
        [Description("Whether users can dismiss the message themselves. Omit for GitLab's default.")]
        bool? dismissable = null,
        [Description(
            "Visual theme: \"indigo\", \"light_indigo\", \"blue\", \"light_blue\", \"green\", \"light_green\", \"red\", \"light_red\", \"dark\", or \"light\". Omit for GitLab's default.")]
        string? theme = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateBroadcastMessageRequest
        {
            Message = message,
            StartsAt = ParseInstant(startsAt, nameof(startsAt)),
            EndsAt = ParseInstant(endsAt, nameof(endsAt)),
            Color = color,
            Font = font,
            TargetAccessLevels = ParseIntList(targetAccessLevels, nameof(targetAccessLevels)),
            TargetPath = targetPath,
            BroadcastType = ParseBroadcastMessageType(broadcastType),
            Dismissable = dismissable,
            Theme = ParseBroadcastMessageTheme(theme)
        };

        var created = await broadcastMessages.CreateAsync(request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(created), "broadcast_messages (create)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_broadcast_message
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_broadcast_message", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates a broadcast message; fields left unset (omitted) keep their current value. Requires instance administrator access.")]
    public async Task<CallToolResult> UpdateBroadcastMessageAsync(
        [Description("The broadcast message's numeric id, from gitlab_list_broadcast_messages.")]
        long broadcastMessageId,
        [Description("The message body shown to users. Omit to leave unchanged.")]
        string? message = null,
        [Description("When the message starts being shown. ISO 8601. Omit to leave unchanged.")]
        string? startsAt = null,
        [Description("When the message stops being shown. ISO 8601. Omit to leave unchanged.")]
        string? endsAt = null,
        [Description("Background color as a CSS hex value. Omit to leave unchanged.")]
        string? color = null,
        [Description("Font color as a CSS hex value. Omit to leave unchanged.")]
        string? font = null,
        [Description(
            "Restricts the message to users whose role is in this comma-separated set of access level integers (10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner). Omit to leave unchanged.")]
        string? targetAccessLevels = null,
        [Description("Restricts the message to pages whose path matches this glob. Omit to leave unchanged.")]
        string? targetPath = null,
        [Description("Presentation style: \"banner\" or \"notification\". Omit to leave unchanged.")]
        string? broadcastType = null,
        [Description("Whether users can dismiss the message themselves. Omit to leave unchanged.")]
        bool? dismissable = null,
        [Description(
            "Visual theme: \"indigo\", \"light_indigo\", \"blue\", \"light_blue\", \"green\", \"light_green\", \"red\", \"light_red\", \"dark\", or \"light\". Omit to leave unchanged.")]
        string? theme = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateBroadcastMessageRequest
        {
            Message = message,
            StartsAt = ParseInstant(startsAt, nameof(startsAt)),
            EndsAt = ParseInstant(endsAt, nameof(endsAt)),
            Color = color,
            Font = font,
            TargetAccessLevels = ParseIntList(targetAccessLevels, nameof(targetAccessLevels)),
            TargetPath = targetPath,
            BroadcastType = ParseBroadcastMessageType(broadcastType),
            Dismissable = dismissable,
            Theme = ParseBroadcastMessageTheme(theme)
        };

        var updated = await broadcastMessages.UpdateAsync(broadcastMessageId, request, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(updated), "broadcast_messages/:id (update)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_broadcast_message
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_broadcast_message", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a broadcast message, and returns it as it was just before deletion. Requires instance administrator access.")]
    public async Task<CallToolResult> DeleteBroadcastMessageAsync(
        [Description("The broadcast message's numeric id, from gitlab_list_broadcast_messages.")]
        long broadcastMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = await broadcastMessages.DeleteAsync(broadcastMessageId, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(message), "broadcast_messages/:id (delete)");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_sidekiq_metrics
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_sidekiq_metrics", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets combined Sidekiq queue, worker-process and job-completion metrics for the instance in one call. GitLab's OpenAPI spec declares no fixed schema for this response, so it is returned as GitLab produced it rather than as a projected shape. Requires instance administrator access.")]
    public async Task<CallToolResult> GetSidekiqMetricsAsync(CancellationToken cancellationToken)
    {
        var metrics = await sidekiq.GetCompoundMetricsAsync(cancellationToken);
        return WrapJsonPayload(metrics, "sidekiq/compound_metrics");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_sidekiq_queue_metrics
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_sidekiq_queue_metrics", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets backlog size and latency for every Sidekiq job queue, for a targeted queue-health check. gitlab_get_sidekiq_metrics also includes process and job-completion metrics alongside this. GitLab's OpenAPI spec declares no fixed schema for this response, so it is returned as GitLab produced it. Requires instance administrator access.")]
    public async Task<CallToolResult> GetSidekiqQueueMetricsAsync(CancellationToken cancellationToken)
    {
        var metrics = await sidekiq.GetQueueMetricsAsync(cancellationToken);
        return WrapJsonPayload(metrics, "sidekiq/queue_metrics");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_delete_sidekiq_queue_jobs
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_delete_sidekiq_queue_jobs", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes every job in a Sidekiq queue whose metadata matches every filter that is set below. At least one filter is required -- GitLab refuses a request with none set, to prevent an accidental whole-queue wipe. Requires instance administrator access.")]
    public async Task<SidekiqQueueJobDeleteResult> DeleteSidekiqQueueJobsAsync(
        [Description("The Sidekiq queue's name, e.g. \"mailers\" or \"default\".")]
        string queueName,
        [Description("Filter: the job's recorded top-level namespace/organization id.")]
        string? organizationId = null,
        [Description("Filter: the job's recorded username.")]
        string? user = null,
        [Description("Filter: the job's recorded user id.")]
        string? userId = null,
        [Description("Filter: the job's recorded GitLab user id (gl_user_id metadata key).")]
        string? glUserId = null,
        [Description("Filter: the job's recorded impersonating/scoped username.")]
        string? scopedUser = null,
        [Description("Filter: the job's recorded impersonating/scoped user id.")]
        string? scopedUserId = null,
        [Description("Filter: the job's recorded project path or id.")]
        string? project = null,
        [Description("Filter: the job's recorded root namespace (top-level group) path.")]
        string? rootNamespace = null,
        [Description("Filter: the job's recorded root namespace id (gl_root_namespace_id metadata key).")]
        string? glRootNamespaceId = null,
        [Description("Filter: the job's recorded OAuth/API client id.")]
        string? clientId = null,
        [Description("Filter: the job's recorded caller id (which endpoint or worker enqueued it).")]
        string? callerId = null,
        [Description("Filter: the job's recorded originating IP address.")]
        string? remoteIp = null,
        [Description("Filter: the job's recorded CI/CD job id.")]
        string? jobId = null,
        [Description("Filter: the job's recorded CI/CD pipeline id.")]
        string? pipelineId = null,
        [Description("Filter: the job's recorded related class name.")]
        string? relatedClass = null,
        [Description("Filter: the job's recorded feature category.")]
        string? featureCategory = null,
        [Description("Filter: the job's recorded artifact size.")]
        string? artifactSize = null,
        [Description("Filter: the job's recorded artifact-used-CDN flag.")]
        string? artifactUsedCdn = null,
        [Description("Filter: the job's recorded artifact dependencies total size.")]
        string? artifactsDependenciesSize = null,
        [Description("Filter: the job's recorded artifact dependencies count.")]
        string? artifactsDependenciesCount = null,
        [Description("Filter: the job's recorded root caller id.")]
        string? rootCallerId = null,
        [Description("Filter: the job's recorded merge action status.")]
        string? mergeActionStatus = null,
        [Description("Filter: the job's recorded bulk-import entity id.")]
        string? bulkImportEntityId = null,
        [Description("Filter: the job's recorded destination Sidekiq shard (Redis instance).")]
        string? sidekiqDestinationShardRedis = null,
        [Description("Filter: the job's recorded GitLab agent for Kubernetes id.")]
        string? kubernetesAgentId = null,
        [Description("Filter: the job's recorded MVCC manifest identifier.")]
        string? mvccManifest = null,
        [Description("Filter: the job's recorded subscription plan name.")]
        string? subscriptionPlan = null,
        [Description("Filter: the job's recorded AI resource identifier.")]
        string? aiResource = null,
        [Description("Filter: the job's recorded Sidekiq worker class name.")]
        string? workerClass = null,
        CancellationToken cancellationToken = default)
    {
        var options = new SidekiqQueueJobDeleteOptions
        {
            OrganizationId = organizationId,
            User = user,
            UserId = userId,
            GlUserId = glUserId,
            ScopedUser = scopedUser,
            ScopedUserId = scopedUserId,
            Project = project,
            RootNamespace = rootNamespace,
            GlRootNamespaceId = glRootNamespaceId,
            ClientId = clientId,
            CallerId = callerId,
            RemoteIp = remoteIp,
            JobId = jobId,
            PipelineId = pipelineId,
            RelatedClass = relatedClass,
            FeatureCategory = featureCategory,
            ArtifactSize = artifactSize,
            ArtifactUsedCdn = artifactUsedCdn,
            ArtifactsDependenciesSize = artifactsDependenciesSize,
            ArtifactsDependenciesCount = artifactsDependenciesCount,
            RootCallerId = rootCallerId,
            MergeActionStatus = mergeActionStatus,
            BulkImportEntityId = bulkImportEntityId,
            SidekiqDestinationShardRedis = sidekiqDestinationShardRedis,
            KubernetesAgentId = kubernetesAgentId,
            MvccManifest = mvccManifest,
            SubscriptionPlan = subscriptionPlan,
            AiResource = aiResource,
            WorkerClass = workerClass
        };

        if (!HasAnyFilter(options))
            throw new McpException("At least one filter must be set, to prevent an accidental whole-queue wipe.");

        await sidekiq.DeleteQueueJobsAsync(queueName, options, cancellationToken);
        return new SidekiqQueueJobDeleteResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_service_ping
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_service_ping", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the full Service Ping payload the instance would submit to GitLab, for reviewing what usage data leaves the instance. The payload is large and semi-structured with no fixed schema across GitLab versions, so it is returned as GitLab produced it rather than as a projected shape. Requires instance administrator access.")]
    public async Task<CallToolResult> GetServicePingAsync(CancellationToken cancellationToken)
    {
        var payload = await usageData.GetServicePingAsync(cancellationToken);
        return WrapJsonPayload(payload, "usage_data/service_ping");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_service_ping_metric_definitions
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_service_ping_metric_definitions", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Downloads every Service Ping metric definition known to the instance: what each metric measures and how it is computed. No fixed schema across GitLab versions, so it is returned as GitLab produced it. Requires instance administrator access.")]
    public async Task<CallToolResult> GetServicePingMetricDefinitionsAsync(
        [Description(
            "Whether each definition includes the source file path(s) it was defined in. Omit for GitLab's default.")]
        bool? includePaths = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await usageData.GetMetricDefinitionsAsync(includePaths, cancellationToken);
        return WrapJsonPayload(payload, "usage_data/metric_definitions");
    }

    // ---- backlog-split/admin/part-5.json below ----

    // ---------------------------------------------------------------------------------------------
    // gitlab_track_usage_events
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_track_usage_events", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Tracks one or more internal GitLab usage events for product analytics. Always calls the batch endpoint, so tracking a single event is simply a one-element batch. Requires instance administrator access.")]
    public async Task<UsageEventsTrackResult> TrackUsageEventsAsync(
        [Description(
            "The events to track, as a JSON array of objects, e.g. [{\"event\":\"i_source_code_commit\",\"projectId\":42}]. Each object requires \"event\" (the internal event name, a string) and may also set \"namespaceId\" (number), \"projectId\" (number), \"projectPath\" (string), \"sendToSnowplow\" (bool), and \"additionalProperties\" (a nested JSON object of extra context key/value pairs).")]
        string eventsJson,
        CancellationToken cancellationToken = default)
    {
        var events = ParseTrackEvents(eventsJson, nameof(eventsJson));
        await usageData.TrackEventsAsync(new TrackEventsRequest { Events = events }, cancellationToken);
        return new UsageEventsTrackResult(events.Count);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_background_migrations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_background_migrations", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists batched background database migrations on the instance (bounded) -- the mechanism Rails uses to run large schema/data changes in small batches over time rather than as one blocking transaction. Typically checked during and after a major GitLab version upgrade. Requires instance administrator access.")]
    public async Task<CallToolResult> ListBackgroundMigrationsAsync(
        [Description(
            "Optional filter by Rails database: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        [Description("Optional filter by the migration's background job class name.")]
        string? jobClassName = null,
        [Description("Maximum migrations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ValidateLimit(limit);

        var options = new BatchedBackgroundMigrationListOptions
        {
            Database = ParseDatabase(database),
            JobClassName = jobClassName
        };

        List<BackgroundMigrationSummary> collected = [];
        var truncated = false;
        await foreach (var migration in backgroundMigrations.ListMigrationsAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(AdminMapper.ToSummary(migration));
        }

        return GitLabContent.Wrap(new BackgroundMigrationListResult(collected, truncated),
            "admin/batched_background_migrations");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_background_migration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_background_migration", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets one batched background database migration by id, including its progress. Requires instance administrator access.")]
    public async Task<CallToolResult> GetBackgroundMigrationAsync(
        [Description("The migration's numeric id, from gitlab_list_background_migrations.")]
        long migrationId,
        [Description(
            "Optional filter naming the Rails database the migration belongs to: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var options = new BatchedBackgroundMigrationGetOptions { Database = ParseDatabase(database) };
        var migration = await backgroundMigrations.GetMigrationAsync(migrationId, options, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(migration), "admin/batched_background_migrations/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_control_background_migration
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_control_background_migration", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Pauses an active batched background migration, or resumes a paused one. GitLab rejects the request with a validation error if the migration is not currently in the state the requested action requires. Requires instance administrator access.")]
    public async Task<CallToolResult> ControlBackgroundMigrationAsync(
        [Description("The migration's numeric id, from gitlab_list_background_migrations.")]
        long migrationId,
        [Description(
            "The action to take: \"pause\" (only valid while the migration is active) or \"resume\" (only valid while the migration is paused).")]
        string action,
        [Description(
            "Optional filter naming the Rails database the migration belongs to: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.ToLowerInvariant();
        var request = new BackgroundJobDatabaseRequest { Database = ParseDatabase(database) };

        GitLabBatchedBackgroundMigration migration;
        switch (normalizedAction)
        {
            case "pause":
                migration = await backgroundMigrations.PauseMigrationAsync(migrationId, request, cancellationToken);
                break;
            case "resume":
                migration = await backgroundMigrations.ResumeMigrationAsync(migrationId, request, cancellationToken);
                break;
            default:
                throw new McpException("action must be \"pause\" or \"resume\".");
        }

        return GitLabContent.Wrap(AdminMapper.ToSummary(migration),
            $"admin/batched_background_migrations/:id/{normalizedAction}");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_background_migration_operation
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_background_migration_operation", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Gets one batched background operation by id -- the finer-grained unit a batched background migration is made of. Requires instance administrator access.")]
    public async Task<CallToolResult> GetBackgroundMigrationOperationAsync(
        [Description("The operation's numeric id, from gitlab_list_background_migration_operations.")]
        long operationId,
        [Description(
            "Optional filter naming the Rails database the operation belongs to: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var options = new BatchedBackgroundOperationGetOptions { Database = ParseDatabase(database) };
        var operation = await backgroundMigrations.GetOperationAsync(operationId, options, cancellationToken);
        return GitLabContent.Wrap(AdminMapper.ToSummary(operation), "admin/batched_background_operations/:id");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_control_background_migration_operation
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_control_background_migration_operation", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Restarts a stopped batched background operation, or stops a queued, active, or paused one. GitLab rejects the request with a validation error if the operation is not currently in the state the requested action requires. Requires instance administrator access.")]
    public async Task<CallToolResult> ControlBackgroundMigrationOperationAsync(
        [Description("The operation's numeric id, from gitlab_list_background_migration_operations.")]
        long operationId,
        [Description(
            "The action to take: \"restart\" (only valid while the operation is stopped) or \"stop\" (valid while the operation is queued, active, or paused).")]
        string action,
        [Description(
            "Optional filter naming the Rails database the operation belongs to: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.ToLowerInvariant();
        var request = new BackgroundJobDatabaseRequest { Database = ParseDatabase(database) };

        GitLabBatchedBackgroundOperation operation;
        switch (normalizedAction)
        {
            case "restart":
                operation = await backgroundMigrations.RestartOperationAsync(operationId, request, cancellationToken);
                break;
            case "stop":
                operation = await backgroundMigrations.StopOperationAsync(operationId, request, cancellationToken);
                break;
            default:
                throw new McpException("action must be \"restart\" or \"stop\".");
        }

        return GitLabContent.Wrap(AdminMapper.ToSummary(operation),
            $"admin/batched_background_operations/:id/{normalizedAction}");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_list_pending_migrations
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_list_pending_migrations", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Lists every pending (not yet run) Rails database migration for the instance. GitLab's OpenAPI spec declares no fixed schema for this response, so it is returned as GitLab produced it rather than as a projected shape -- despite the name, this is a single JSON payload, not a paginated stream, and is not affected by a limit parameter. Requires instance administrator access.")]
    public async Task<CallToolResult> ListPendingMigrationsAsync(
        [Description(
            "Optional filter by Rails database: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var options = new AdminMigrationListOptions { Database = ParseDatabase(database) };
        var payload = await adminMigrations.ListPendingAsync(options, cancellationToken);
        return WrapJsonPayload(payload, "admin/migrations/pending");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_mark_migration_applied
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_mark_migration_applied", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Marks a pending Rails database migration, identified by its version timestamp, as successfully applied without actually running it, so future db:migrate tasks skip it. Use with care -- this does not run the migration's actual schema/data change. GitLab rejects the request with a validation error if the migration is not currently pending. Requires instance administrator access.")]
    public async Task<MigrationMarkAppliedResult> MarkMigrationAppliedAsync(
        [Description(
            "The migration's version timestamp, e.g. 20231201000000, exactly as it appears in gitlab_list_pending_migrations.")]
        long timestamp,
        [Description(
            "Optional filter naming the Rails database the migration belongs to: \"main\", \"ci\", \"sec\", \"embedding\", or \"geo\". Omit for the default (\"main\").")]
        string? database = null,
        CancellationToken cancellationToken = default)
    {
        var request = new BackgroundJobDatabaseRequest { Database = ParseDatabase(database) };
        await adminMigrations.MarkAppliedAsync(timestamp, request, cancellationToken);
        return new MigrationMarkAppliedResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_api_documentation
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_api_documentation", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the Swagger/OpenAPI description of GitLab's whole API, or of one mounted API area when an area name is given, for introspecting what a specific instance's API surface looks like. GitLab's OpenAPI spec declares no fixed response schema, so it is returned as GitLab produced it.")]
    public async Task<CallToolResult> GetApiDocumentationAsync(
        [Description(
            "Optional mounted API area name to scope the description to, e.g. \"v4\". Omit to get the description of the whole API.")]
        string? area = null,
        [Description("Locale for the description. Only used when area is set; omit for GitLab's default (\"en\").")]
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        var payload = area is null
            ? await internalClient.GetSwaggerDocumentationAsync(cancellationToken)
            : await internalClient.GetSwaggerDocumentationAsync(area, string.IsNullOrWhiteSpace(locale) ? "en" : locale,
                cancellationToken);

        return WrapJsonPayload(payload, area is null ? "swagger_doc" : "swagger_doc/:name");
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_get_compliance_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_get_compliance_settings", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets the instance's centralized security policies (CSP) namespace setting: the id of the group whose compliance frameworks and policies apply instance-wide. Requires administrator access.")]
    public async Task<ComplianceSettingsSummary> GetComplianceSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await complianceSettings.GetAsync(cancellationToken);
        return AdminMapper.ToSummary(settings);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_update_compliance_settings
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_update_compliance_settings", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Sets the instance's centralized security policies (CSP) namespace: the group whose compliance frameworks and policies apply instance-wide. Requires administrator access.")]
    public async Task<ComplianceSettingsSummary> UpdateComplianceSettingsAsync(
        [Description("The numeric id of the group to designate as the instance's CSP namespace.")]
        long cspNamespaceId,
        CancellationToken cancellationToken = default)
    {
        var settings = await complianceSettings.UpdateAsync(
            new UpdateCompliancePolicySettingsRequest { CspNamespaceId = cspNamespaceId }, cancellationToken);
        return AdminMapper.ToSummary(settings);
    }

    // ---------------------------------------------------------------------------------------------
    // gitlab_set_compliance_control_status
    // ---------------------------------------------------------------------------------------------

    [McpServerTool(Name = "gitlab_set_compliance_control_status", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Reports the pass/fail outcome of a third-party compliance check for one of a project's external control bindings, e.g. from a CI pipeline step.")]
    public async Task<ComplianceControlStatusResult> SetComplianceControlStatusAsync(
        [Description("Project: numeric id or \"namespace/path\".")]
        string project,
        [Description("The external control's numeric id, as configured on the project's compliance framework.")]
        long controlId,
        [Description("The check's outcome: \"pass\" or \"fail\".")]
        string status,
        CancellationToken cancellationToken = default)
    {
        var request = new SetComplianceExternalControlStatusRequest { Status = ParseComplianceControlStatus(status) };
        await complianceSettings.SetExternalControlStatusAsync(project, controlId, request, cancellationToken);
        return new ComplianceControlStatusResult(true);
    }

    // ---------------------------------------------------------------------------------------------
    // Validation / parsing helpers
    // ---------------------------------------------------------------------------------------------

    private static void ValidateLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");
    }

    private static Uri? ParseUriOptional(string? url, string paramName)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return parsed;

        throw new McpException($"{paramName} must be an absolute URL, e.g. \"https://example.com\", or omitted.");
    }

    private static GitLabVisibility? ParseVisibility(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Enum.TryParse<GitLabVisibility>(value, true, out var parsed)) return parsed;

        throw new McpException($"{paramName} must be \"private\", \"internal\", \"public\", or omitted.");
    }

    private static GitLabEmailConfirmationSetting? ParseEmailConfirmationSetting(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Enum.TryParse<GitLabEmailConfirmationSetting>(value, true, out var parsed)) return parsed;

        throw new McpException($"{paramName} must be \"off\", \"soft\", \"hard\", or omitted.");
    }

    private static GitLabGitAccessProtocol? ParseGitAccessProtocol(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (Enum.TryParse<GitLabGitAccessProtocol>(value, true, out var parsed)) return parsed;

        throw new McpException($"{paramName} must be \"ssh\", \"http\", \"all\", or omitted.");
    }

    private static GitLabPlanName ParsePlanName(string value, string paramName)
    {
        // Accept GitLab's own snake_case spelling ("ultimate_trial") as well as the bare enum name
        // ("UltimateTrial") -- TryParse alone only recognizes the latter.
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<GitLabPlanName>(normalized, true, out var parsed)) return parsed;

        throw new McpException(
            $"{paramName} must be one of: free, premium, ultimate, default, bronze, silver, gold, opensource, "
            + "ultimate_trial, premium_trial, ultimate_trial_paid_customer (case-insensitive).");
    }

    private static Uri ParseUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return parsed;

        throw new McpException("url must be an absolute URL, e.g. \"https://example.com/webhook\".");
    }

    private static GitLabHookBranchFilterStrategy? ParseBranchFilterStrategy(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "wildcard" => GitLabHookBranchFilterStrategy.Wildcard,
            "regex" => GitLabHookBranchFilterStrategy.Regex,
            "all_branches" => GitLabHookBranchFilterStrategy.AllBranches,
            _ => throw new McpException(
                "branchFilterStrategy must be \"wildcard\", \"regex\", \"all_branches\", or omitted.")
        };
    }

    private static GitLabManagedLicenseApprovalStatus ParseApprovalStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "allowed" => GitLabManagedLicenseApprovalStatus.Allowed,
            "denied" => GitLabManagedLicenseApprovalStatus.Denied,
            _ => throw new McpException("approvalStatus must be \"allowed\" or \"denied\".")
        };
    }

    private static GitLabBackgroundJobDatabase? ParseDatabase(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "main" => GitLabBackgroundJobDatabase.Main,
            "ci" => GitLabBackgroundJobDatabase.Ci,
            "sec" => GitLabBackgroundJobDatabase.Sec,
            "embedding" => GitLabBackgroundJobDatabase.Embedding,
            "geo" => GitLabBackgroundJobDatabase.Geo,
            _ => throw new McpException(
                "database must be \"main\", \"ci\", \"sec\", \"embedding\", \"geo\", or omitted.")
        };
    }

    private static DateTimeOffset? ParseInstant(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) return parsed;

        throw new McpException(
            $"{paramName} must be an ISO 8601 date or date-time (e.g. \"2025-01-01\" or \"2025-01-01T00:00:00Z\").");
    }

    private static IReadOnlyList<string>? ParseStatusList(string? commaSeparated)
    {
        return string.IsNullOrWhiteSpace(commaSeparated)
            ? null
            : commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static JsonElement ParseJsonElement(string json, string paramName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new McpException($"{paramName} must be valid JSON: {ex.Message}");
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseSettingsObject(string json, string paramName)
    {
        var element = ParseJsonElement(json, paramName);
        if (element.ValueKind != JsonValueKind.Object) throw new McpException($"{paramName} must be a JSON object.");

        var settings = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) settings[property.Name] = property.Value;

        return settings;
    }

    private static GitLabWebhookTestTrigger ParseWebhookTestTrigger(string value, string paramName)
    {
        // Accept GitLab's own snake_case spelling ("push_events") as well as the bare enum name
        // ("PushEvents") -- TryParse alone only recognizes the latter.
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<GitLabWebhookTestTrigger>(normalized, true, out var parsed)) return parsed;

        throw new McpException(
            $"{paramName} must be one of: push_events, issues_events, confidential_issues_events, "
            + "merge_requests_events, note_events, confidential_note_events, job_events, pipeline_events, "
            + "deployment_events, feature_flag_events, milestone_events, emoji_events (case-insensitive).");
    }

    private static IReadOnlyList<GitLabHookUrlVariable>? ParseUrlVariables(string? json, string paramName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var settings = ParseSettingsObject(json, paramName);
        List<GitLabHookUrlVariable> variables = [];
        foreach (var (key, value) in settings)
        {
            if (value.ValueKind != JsonValueKind.String)
                throw new McpException($"{paramName}: the value for \"{key}\" must be a JSON string.");

            variables.Add(new GitLabHookUrlVariable { Key = key, Value = value.GetString()! });
        }

        return variables;
    }

    private static IReadOnlyList<GitLabHookCustomHeader>? ParseCustomHeaders(string? json, string paramName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var settings = ParseSettingsObject(json, paramName);
        List<GitLabHookCustomHeader> headers = [];
        foreach (var (key, value) in settings)
        {
            if (value.ValueKind != JsonValueKind.String)
                throw new McpException($"{paramName}: the value for \"{key}\" must be a JSON string.");

            headers.Add(new GitLabHookCustomHeader { Key = key, Value = value.GetString()! });
        }

        return headers;
    }

    // ---- backlog-split/admin/part-3.json helpers ----

    private static GitLabTokenExchangeAudience ParseTokenExchangeAudience(string value, string paramName)
    {
        return value.ToLowerInvariant() switch
        {
            "artifact_registry" => GitLabTokenExchangeAudience.GitlabArtifactRegistry,
            _ => throw new McpException(
                $"{paramName} must be \"artifact_registry\" (the only value GitLab currently accepts).")
        };
    }

    private static string? ParsePercentageKey(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "percentage_of_actors" => "percentage_of_actors",
            "percentage_of_time" => "percentage_of_time",
            _ => throw new McpException(
                "percentageKey must be \"percentage_of_actors\", \"percentage_of_time\", or omitted.")
        };
    }

    private static GitLabFeatureFlagState? ParseFeatureFlagState(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "enabled" => GitLabFeatureFlagState.Enabled,
            "disabled" => GitLabFeatureFlagState.Disabled,
            _ => throw new McpException("scope must be \"enabled\", \"disabled\", or omitted.")
        };
    }

    private static GitLabMinimumRole ParseMinimumRole(string value)
    {
        // Accept GitLab's own snake_case spelling ("no_one_allowed") as well as the bare enum name
        // ("NoOneAllowed") -- TryParse alone only recognizes the latter.
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<GitLabMinimumRole>(normalized, true, out var parsed)) return parsed;

        throw new McpException(
            "minimumRole must be one of: no_one_allowed, developer, maintainer, owner (case-insensitive).");
    }

    private static IReadOnlyList<FeatureFlagStrategyRequest>? ParseStrategies(string? json, string paramName)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var element = ParseJsonElement(json, paramName);
        if (element.ValueKind != JsonValueKind.Array) throw new McpException($"{paramName} must be a JSON array.");

        List<FeatureFlagStrategyRequest> strategies = [];
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new McpException($"{paramName}: each element must be a JSON object.");

            var strategy = new FeatureFlagStrategyRequest();

            if (item.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.Number)
                strategy = strategy with { Id = idElement.GetInt64() };

            if (item.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                strategy = strategy with { Name = nameElement.GetString() };

            if (item.TryGetProperty("parameters", out var parametersElement))
                strategy = strategy with { Parameters = parametersElement.Clone() };

            if (item.TryGetProperty("userListId", out var userListIdElement) &&
                userListIdElement.ValueKind == JsonValueKind.Number)
                strategy = strategy with { UserListId = userListIdElement.GetInt64() };

            if (item.TryGetProperty("destroy", out var destroyElement) &&
                destroyElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                strategy = strategy with { Destroy = destroyElement.GetBoolean() };

            if (item.TryGetProperty("scopes", out var scopesElement))
            {
                if (scopesElement.ValueKind != JsonValueKind.Array)
                    throw new McpException($"{paramName}: \"scopes\" must be a JSON array.");

                List<FeatureFlagStrategyScopeRequest> scopes = [];
                foreach (var scopeItem in scopesElement.EnumerateArray())
                {
                    if (scopeItem.ValueKind != JsonValueKind.Object
                        || !scopeItem.TryGetProperty("environmentScope", out var envElement)
                        || envElement.ValueKind != JsonValueKind.String)
                        throw new McpException($"{paramName}: each scope must be {{\"environmentScope\":\"...\"}}.");

                    scopes.Add(new FeatureFlagStrategyScopeRequest { EnvironmentScope = envElement.GetString() });
                }

                strategy = strategy with { Scopes = scopes };
            }

            strategies.Add(strategy);
        }

        return strategies;
    }

    // ---- backlog-split/admin/part-4.json helpers ----

    private static IReadOnlyList<int>? ParseIntList(string? commaSeparated, string paramName)
    {
        if (string.IsNullOrWhiteSpace(commaSeparated)) return null;

        var parts = commaSeparated.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<int> values = [];
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new McpException($"{paramName} must be a comma-separated list of integers, e.g. \"30,40\".");

            values.Add(value);
        }

        return values;
    }

    private static GitLabBroadcastMessageType? ParseBroadcastMessageType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "banner" => GitLabBroadcastMessageType.Banner,
            "notification" => GitLabBroadcastMessageType.Notification,
            _ => throw new McpException("broadcastType must be \"banner\", \"notification\", or omitted.")
        };
    }

    private static GitLabBroadcastMessageTheme? ParseBroadcastMessageTheme(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            null or "" => null,
            "indigo" => GitLabBroadcastMessageTheme.Indigo,
            "light_indigo" => GitLabBroadcastMessageTheme.LightIndigo,
            "blue" => GitLabBroadcastMessageTheme.Blue,
            "light_blue" => GitLabBroadcastMessageTheme.LightBlue,
            "green" => GitLabBroadcastMessageTheme.Green,
            "light_green" => GitLabBroadcastMessageTheme.LightGreen,
            "red" => GitLabBroadcastMessageTheme.Red,
            "light_red" => GitLabBroadcastMessageTheme.LightRed,
            "dark" => GitLabBroadcastMessageTheme.Dark,
            "light" => GitLabBroadcastMessageTheme.Light,
            _ => throw new McpException(
                "theme must be one of: indigo, light_indigo, blue, light_blue, green, light_green, red, light_red, dark, light, or omitted.")
        };
    }

    /// <summary>
    ///     Serializes a raw, schema-less GitLab JSON payload (Sidekiq metrics, Service Ping) as text and
    ///     wraps it via <see cref="GitLabContent" /> -- these payloads have no fixed shape across GitLab
    ///     versions (see e.g. <c>ISidekiqClient</c>'s own type doc), and a per-domain
    ///     <see cref="System.Text.Json.Serialization.JsonSerializerContext" /> in Metadata mode has no
    ///     generated metadata for an open-ended <see cref="JsonElement" /> tree, so a projection record cannot
    ///     carry one as a field. Truncated at <see cref="MaxJsonPayloadChars" /> characters, stated in the
    ///     body when it happens.
    /// </summary>
    private static CallToolResult WrapJsonPayload(JsonElement payload, string source)
    {
        var text = payload.GetRawText();
        var body = text.Length > MaxJsonPayloadChars
            ? $"[payload truncated at {MaxJsonPayloadChars} characters]{Environment.NewLine}{text[..MaxJsonPayloadChars]}"
            : text;

        return GitLabContent.WrapText(body, source);
    }

    // ---- backlog-split/admin/part-5.json helpers ----

    private static List<TrackEventRequest> ParseTrackEvents(string json, string paramName)
    {
        var element = ParseJsonElement(json, paramName);
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
            throw new McpException($"{paramName} must be a non-empty JSON array of event objects.");

        List<TrackEventRequest> events = [];
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("event", out var eventNameElement)
                || eventNameElement.ValueKind != JsonValueKind.String)
                throw new McpException(
                    $"{paramName}: each element must be a JSON object with a string \"event\" property.");

            var trackEvent = new TrackEventRequest { Event = eventNameElement.GetString()! };

            if (item.TryGetProperty("namespaceId", out var namespaceIdElement) &&
                namespaceIdElement.ValueKind == JsonValueKind.Number)
                trackEvent = trackEvent with { NamespaceId = namespaceIdElement.GetInt64() };

            if (item.TryGetProperty("projectId", out var projectIdElement) &&
                projectIdElement.ValueKind == JsonValueKind.Number)
                trackEvent = trackEvent with { ProjectId = projectIdElement.GetInt64() };

            if (item.TryGetProperty("projectPath", out var projectPathElement) &&
                projectPathElement.ValueKind == JsonValueKind.String)
                trackEvent = trackEvent with { ProjectPath = projectPathElement.GetString() };

            if (item.TryGetProperty("sendToSnowplow", out var snowplowElement) &&
                snowplowElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
                trackEvent = trackEvent with { SendToSnowplow = snowplowElement.GetBoolean() };

            if (item.TryGetProperty("additionalProperties", out var additionalElement) &&
                additionalElement.ValueKind == JsonValueKind.Object)
            {
                var additional = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var property in additionalElement.EnumerateObject())
                    additional[property.Name] = property.Value;

                trackEvent = trackEvent with { AdditionalProperties = additional };
            }

            events.Add(trackEvent);
        }

        return events;
    }

    private static GitLabComplianceExternalControlStatus ParseComplianceControlStatus(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pass" => GitLabComplianceExternalControlStatus.Pass,
            "fail" => GitLabComplianceExternalControlStatus.Fail,
            _ => throw new McpException("status must be \"pass\" or \"fail\".")
        };
    }

    private static bool HasAnyFilter(SidekiqQueueJobDeleteOptions options)
    {
        return options.OrganizationId is not null
               || options.User is not null
               || options.UserId is not null
               || options.GlUserId is not null
               || options.ScopedUser is not null
               || options.ScopedUserId is not null
               || options.Project is not null
               || options.RootNamespace is not null
               || options.GlRootNamespaceId is not null
               || options.ClientId is not null
               || options.CallerId is not null
               || options.RemoteIp is not null
               || options.JobId is not null
               || options.PipelineId is not null
               || options.RelatedClass is not null
               || options.FeatureCategory is not null
               || options.ArtifactSize is not null
               || options.ArtifactUsedCdn is not null
               || options.ArtifactsDependenciesSize is not null
               || options.ArtifactsDependenciesCount is not null
               || options.RootCallerId is not null
               || options.MergeActionStatus is not null
               || options.BulkImportEntityId is not null
               || options.SidekiqDestinationShardRedis is not null
               || options.KubernetesAgentId is not null
               || options.MvccManifest is not null
               || options.SubscriptionPlan is not null
               || options.AiResource is not null
               || options.WorkerClass is not null;
    }
}