using System.Text.Json;
using GitLab.Client.Models;
using GitlabMCP.Contracts.Admin;

namespace GitlabMCP.Mapping.Admin;

/// <summary>
///     Projects the "admin" domain's GitLab.Client DTOs into the owned records under
///     <see cref="GitlabMCP.Contracts.Admin" />.
/// </summary>
public static class AdminMapper
{
    private const int ResponseBodyPreviewLimit = 500;

    // --- Instance appearance ---

    public static InstanceAppearanceSummary ToSummary(GitLabAppearance appearance)
    {
        return new InstanceAppearanceSummary(
            appearance.Title,
            appearance.Description,
            appearance.PwaName,
            appearance.PwaShortName,
            appearance.PwaDescription,
            appearance.Logo?.ToString(),
            appearance.PwaIcon?.ToString(),
            appearance.HeaderLogo?.ToString(),
            appearance.Favicon?.ToString(),
            appearance.NewProjectGuidelines,
            appearance.MemberGuidelines,
            appearance.ProfileImageGuidelines,
            appearance.HeaderMessage,
            appearance.FooterMessage,
            appearance.MessageBackgroundColor,
            appearance.MessageFontColor,
            appearance.EmailHeaderAndFooterEnabled,
            appearance.SiteName);
    }

    // --- Instance metadata, statistics, settings, plan limits ---

    public static MetadataSummary ToSummary(GitLabMetadata metadata)
    {
        return new MetadataSummary(
            metadata.Version,
            metadata.Revision,
            metadata.Enterprise,
            metadata.Kas?.Enabled,
            metadata.Kas?.Version,
            metadata.Kas?.ExternalUrl);
    }

    public static InstanceStatisticsSummary ToSummary(GitLabApplicationStatistics statistics)
    {
        return new InstanceStatisticsSummary(
            statistics.Forks,
            statistics.Issues,
            statistics.MergeRequests,
            statistics.Notes,
            statistics.Snippets,
            statistics.SshKeys,
            statistics.Milestones,
            statistics.Users,
            statistics.Projects,
            statistics.Groups,
            statistics.ActiveUsers);
    }

    public static InstanceSettingsSummary ToSummary(GitLabApplicationSettings settings)
    {
        return new InstanceSettingsSummary(
            settings.Id,
            settings.PerformanceBarAllowedGroupId,
            settings.AbuseNotificationEmail,
            settings.AdminMode,
            settings.AfterSignOutPath,
            settings.AfterSignUpText,
            settings.AkismetEnabled,
            settings.AllowLocalRequestsFromWebHooksAndServices,
            settings.AllowLocalRequestsFromSystemHooks,
            settings.AllowPossibleSpam,
            settings.DnsRebindingProtectionEnabled,
            settings.ArchiveBuildsInHumanReadable,
            settings.AssetProxyEnabled,
            settings.AssetProxyUrl,
            settings.AssetProxyAllowlist,
            settings.StaticObjectsExternalStorageUrl,
            settings.AuthnDataRetentionCleanupEnabled,
            settings.AuthorizedKeysEnabled,
            settings.AutoDevopsEnabled,
            settings.AutoDevopsDomain,
            settings.AutocompleteUsersLimit,
            settings.AutocompleteUsersUnauthenticatedLimit,
            settings.AllowBypassPlaceholderConfirmation,
            settings.CiDeletePipelinesInSecondsLimitHumanReadable,
            settings.CiJobLiveTraceEnabled,
            settings.CiPartitionsInSecondsLimitHumanReadable,
            settings.ConcurrentGithubImportJobsLimit,
            settings.ConcurrentBitbucketImportJobsLimit,
            settings.ConcurrentBitbucketServerImportJobsLimit,
            settings.ConcurrentPullRequestImportJobsLimit,
            settings.ImportJobsConcurrencyLimit,
            settings.ContainerExpirationPoliciesEnableHistoricEntries,
            settings.ContainerRegistryExpirationPoliciesCaching,
            settings.ContainerRegistryTokenExpireDelay,
            settings.OauthAccessTokenExpiresIn,
            settings.DecompressArchiveFileTimeout,
            settings.DefaultArtifactsExpireIn,
            settings.DefaultBranchName,
            settings.DefaultBranchProtection,
            settings.DefaultBranchProtectionDefaults,
            settings.DefaultCiConfigPath,
            settings.DefaultGroupVisibility,
            settings.DefaultPreferredLanguage,
            settings.DefaultProjectCreation,
            settings.DefaultProjectVisibility,
            settings.DefaultProjectsLimit,
            settings.DefaultSnippetVisibility,
            settings.DefaultSyntaxHighlightingTheme);
    }

    public static PlanLimitsSummary ToSummary(GitLabPlanLimits limits)
    {
        return new PlanLimitsSummary(
            limits.CargoMaxFileSize,
            limits.CiInstanceLevelVariables,
            limits.CiPipelineSize,
            limits.CiActiveJobs,
            limits.CiProjectSubscriptions,
            limits.CiPipelineSchedules,
            limits.CiNeedsSizeLimit,
            limits.CiRegisteredGroupRunners,
            limits.CiRegisteredProjectRunners,
            limits.ConanMaxFileSize,
            limits.DotenvVariables,
            limits.DotenvSize,
            limits.EnforcementLimit,
            limits.GenericPackagesMaxFileSize,
            limits.HelmMaxFileSize,
            limits.MavenMaxFileSize,
            limits.NotificationLimit,
            limits.NpmMaxFileSize,
            limits.NugetMaxFileSize,
            limits.PipelineHierarchySize,
            limits.PypiMaxFileSize,
            limits.ServiceDeskOutboundEmailsPerHour,
            limits.ServiceDeskOutboundEmailsPerDay,
            limits.TerraformModuleMaxFileSize,
            limits.StorageSizeLimit,
            limits.WebHookCalls,
            limits.WebHookCallsLow,
            limits.WebHookCallsMid,
            limits.MaxPipelinesPerMergeTrain);
    }

    // --- Licenses ---

    public static LicenseSummary ToSummary(GitLabLicense license)
    {
        var (name, company) = ReadLicensee(license.Licensee);
        return new LicenseSummary(
            license.Id,
            license.Plan,
            license.CreatedAt,
            license.StartsAt,
            license.ExpiresAt,
            license.HistoricalMax,
            license.MaximumUserCount,
            name,
            company,
            license.Expired,
            license.Overage,
            license.UserLimit,
            license.ActiveUsers);
    }

    public static LicenseRefreshResult ToSummary(GitLabLicenseRefreshResult result)
    {
        return new LicenseRefreshResult(
            result.Success.HasValue && result.Success.Value.ValueKind == JsonValueKind.True);
    }

    private static (string? Name, string? Company) ReadLicensee(JsonElement? licensee)
    {
        if (!licensee.HasValue || licensee.Value.ValueKind != JsonValueKind.Object) return (null, null);

        var element = licensee.Value;
        var name = element.TryGetProperty("Name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        var company = element.TryGetProperty("Company", out var companyElement) &&
                      companyElement.ValueKind == JsonValueKind.String
            ? companyElement.GetString()
            : null;

        return (name, company);
    }

    // --- Feature flags ---

    public static FeatureFlagUserListSummary ToSummary(GitLabFeatureFlagUserList list)
    {
        return new FeatureFlagUserListSummary(
            list.Id,
            list.Iid,
            list.Name,
            list.UserXids,
            list.ProjectId,
            list.CreatedAt,
            list.UpdatedAt);
    }

    public static FeatureFlagSummary ToSummary(GitLabFeatureFlag flag)
    {
        return new FeatureFlagSummary(
            flag.Name,
            flag.Description,
            flag.Active,
            flag.Version,
            flag.Strategies?.Select(static s => s.Name ?? "(unnamed)").ToList() ?? [],
            flag.CreatedAt,
            flag.UpdatedAt);
    }

    // --- License policies ---

    public static LicensePolicySummary ToSummary(GitLabManagedLicense license)
    {
        return new LicensePolicySummary(
            license.Id,
            license.Name,
            license.ApprovalStatus);
    }

    // --- Integrations ---

    public static IntegrationSummary ToSummary(GitLabIntegration integration)
    {
        return new IntegrationSummary(
            integration.Id,
            integration.Title,
            integration.Slug,
            integration.Active,
            integration.Inherited,
            integration.CreatedAt,
            integration.UpdatedAt);
    }

    // --- Background migrations ---

    public static BackgroundMigrationSummary ToSummary(GitLabBatchedBackgroundMigration migration)
    {
        return new BackgroundMigrationSummary(
            migration.Id,
            migration.JobClassName,
            migration.TableName,
            migration.ColumnName,
            migration.Status,
            migration.Progress,
            migration.CreatedAt,
            migration.EstimatedTimeRemaining);
    }

    public static BackgroundMigrationOperationSummary ToSummary(GitLabBatchedBackgroundOperation operation)
    {
        return new BackgroundMigrationOperationSummary(
            operation.Id,
            operation.Partition,
            operation.JobClassName,
            operation.TableName,
            operation.ColumnName,
            operation.Status,
            operation.CreatedAt,
            operation.StartedAt,
            operation.FinishedAt,
            operation.OnHoldUntil);
    }

    // --- Audit events ---

    public static AuditEventSummary ToSummary(GitLabAuditEvent auditEvent)
    {
        return new AuditEventSummary(
            auditEvent.Id,
            auditEvent.AuthorId,
            auditEvent.EntityId,
            auditEvent.EntityType,
            auditEvent.EventName,
            auditEvent.CreatedAt);
    }

    // --- Webhook delivery logs ---

    public static HookDeliverySummary ToSummary(GitLabHookEvent delivery)
    {
        var (preview, truncated) = Truncate(delivery.ResponseBody);
        return new HookDeliverySummary(
            delivery.Id,
            delivery.Url?.ToString(),
            delivery.Trigger,
            delivery.ResponseStatus,
            preview,
            truncated,
            delivery.ExecutionDuration,
            delivery.InternalErrorMessage);
    }

    // --- Webhooks ---

    public static HookSummary ToSummary(GitLabProjectHook hook)
    {
        return new HookSummary(
            hook.Id,
            hook.Url?.ToString(),
            hook.Name,
            hook.Description,
            CollectEnabledEvents(
                ("push_events", hook.PushEvents),
                ("issues_events", hook.IssuesEvents),
                ("confidential_issues_events", hook.ConfidentialIssuesEvents),
                ("merge_requests_events", hook.MergeRequestsEvents),
                ("tag_push_events", hook.TagPushEvents),
                ("note_events", hook.NoteEvents),
                ("confidential_note_events", hook.ConfidentialNoteEvents),
                ("job_events", hook.JobEvents),
                ("pipeline_events", hook.PipelineEvents),
                ("wiki_page_events", hook.WikiPageEvents),
                ("deployment_events", hook.DeploymentEvents),
                ("releases_events", hook.ReleasesEvents),
                ("feature_flag_events", hook.FeatureFlagEvents),
                ("milestone_events", hook.MilestoneEvents),
                ("emoji_events", hook.EmojiEvents),
                ("repository_update_events", hook.RepositoryUpdateEvents),
                ("vulnerability_events", hook.VulnerabilityEvents),
                ("resource_access_token_events", hook.ResourceAccessTokenEvents),
                ("resource_deploy_token_events", hook.ResourceDeployTokenEvents),
                ("duo_flow_callback_enabled", hook.DuoFlowCallbackEnabled)),
            hook.TokenPresent,
            hook.SigningTokenPresent,
            hook.UrlVariables?.Select(static v => v.Key ?? "(unnamed)").ToList() ?? [],
            hook.CustomHeaders?.Select(static h => h.Key ?? "(unnamed)").ToList() ?? [],
            hook.AlertStatus,
            hook.CreatedAt);
    }

    public static HookSummary ToSummary(GitLabGroupHook hook)
    {
        return new HookSummary(
            hook.Id,
            hook.Url?.ToString(),
            hook.Name,
            hook.Description,
            CollectEnabledEvents(
                ("push_events", hook.PushEvents),
                ("issues_events", hook.IssuesEvents),
                ("confidential_issues_events", hook.ConfidentialIssuesEvents),
                ("merge_requests_events", hook.MergeRequestsEvents),
                ("tag_push_events", hook.TagPushEvents),
                ("note_events", hook.NoteEvents),
                ("confidential_note_events", hook.ConfidentialNoteEvents),
                ("job_events", hook.JobEvents),
                ("pipeline_events", hook.PipelineEvents),
                ("wiki_page_events", hook.WikiPageEvents),
                ("deployment_events", hook.DeploymentEvents),
                ("releases_events", hook.ReleasesEvents),
                ("subgroup_events", hook.SubgroupEvents),
                ("project_events", hook.ProjectEvents),
                ("member_events", hook.MemberEvents),
                ("feature_flag_events", hook.FeatureFlagEvents),
                ("milestone_events", hook.MilestoneEvents),
                ("emoji_events", hook.EmojiEvents),
                ("repository_update_events", hook.RepositoryUpdateEvents),
                ("vulnerability_events", hook.VulnerabilityEvents),
                ("resource_access_token_events", hook.ResourceAccessTokenEvents),
                ("duo_flow_callback_enabled", hook.DuoFlowCallbackEnabled)),
            hook.TokenPresent,
            hook.SigningTokenPresent,
            hook.UrlVariables?.Select(static v => v.Key ?? "(unnamed)").ToList() ?? [],
            hook.CustomHeaders?.Select(static h => h.Key ?? "(unnamed)").ToList() ?? [],
            hook.AlertStatus,
            hook.CreatedAt);
    }

    public static HookSummary ToSummary(GitLabSystemHook hook)
    {
        return new HookSummary(
            hook.Id,
            hook.Url?.ToString(),
            hook.Name,
            hook.Description,
            CollectEnabledEvents(
                ("push_events", hook.PushEvents),
                ("tag_push_events", hook.TagPushEvents),
                ("merge_requests_events", hook.MergeRequestsEvents),
                ("repository_update_events", hook.RepositoryUpdateEvents)),
            hook.TokenPresent,
            hook.SigningTokenPresent,
            hook.UrlVariables?.Select(static v => v.Key ?? "(unnamed)").ToList() ?? [],
            hook.CustomHeaders?.Select(static h => h.Key ?? "(unnamed)").ToList() ?? [],
            hook.AlertStatus,
            hook.CreatedAt);
    }

    private static IReadOnlyList<string> CollectEnabledEvents(params (string Name, bool? Enabled)[] flags)
    {
        return flags.Where(static f => f.Enabled == true).Select(static f => f.Name).ToList();
    }

    private static (string? Preview, bool Truncated) Truncate(string? text)
    {
        if (text is null) return (null, false);

        return text.Length > ResponseBodyPreviewLimit
            ? (text[..ResponseBodyPreviewLimit], true)
            : (text, false);
    }

    // ---- backlog-split/admin/part-3.json below ----

    // --- Instance features ---

    public static InstanceFeatureSummary ToSummary(GitLabFeature feature)
    {
        return new InstanceFeatureSummary(
            feature.Name,
            feature.State,
            feature.Gates?.Select(static g => new FeatureGateSummary(g.Key, g.Value?.ToString())).ToList() ?? [],
            feature.Definition?.Type,
            feature.Definition?.Group,
            feature.Definition?.Milestone,
            feature.Definition?.DefaultEnabled);
    }

    public static InstanceFeatureDefinitionSummary ToSummary(GitLabFeatureDefinition definition)
    {
        return new InstanceFeatureDefinitionSummary(
            definition.Name,
            definition.Type,
            definition.Group,
            definition.Milestone,
            definition.IntendedToRolloutBy,
            definition.DefaultEnabled,
            definition.LogStateChanges,
            definition.FeatureIssueUrl?.ToString(),
            definition.IntroducedByUrl?.ToString(),
            definition.RolloutIssueUrl?.ToString());
    }

    // --- Project feature flags ---

    public static FeatureFlagSettingsSummary ToSummary(GitLabFeatureFlagSettings settings)
    {
        return new FeatureFlagSettingsSummary(settings.MinimumRole);
    }

    // --- CI job identity (IPlatformIntegrationsClient.GetAllowedAgentsAsync) ---

    public static CiJobAllowedAgentsSummary ToAllowedAgentsSummary(GitLabJob job)
    {
        return new CiJobAllowedAgentsSummary(
            job.Id,
            job.Name,
            job.Status,
            job.Stage,
            job.Ref,
            job.WebUrl?.ToString());
    }

    // ---- backlog-split/admin/part-4.json below ----

    // --- OAuth applications ---

    public static OAuthApplicationSummary ToSummary(GitLabApplication app)
    {
        return new OAuthApplicationSummary(
            app.Id,
            app.ApplicationId,
            app.ApplicationName,
            app.CallbackUrl?.ToString(),
            app.Confidential,
            app.Scopes ?? []);
    }

    public static OAuthApplicationWithSecretSummary ToSummary(GitLabApplicationWithSecret app)
    {
        return new OAuthApplicationWithSecretSummary(
            app.Id,
            app.ApplicationId,
            app.ApplicationName,
            app.CallbackUrl?.ToString(),
            app.Confidential,
            app.Scopes ?? [],
            app.Secret ??
            throw new InvalidOperationException("GitLab did not return the OAuth application's plaintext secret."));
    }

    // --- Broadcast messages ---

    public static BroadcastMessageSummary ToSummary(GitLabBroadcastMessage message)
    {
        return new BroadcastMessageSummary(
            message.Id,
            message.Message,
            message.StartsAt,
            message.EndsAt,
            message.Color,
            message.Font,
            message.TargetAccessLevels ?? [],
            message.TargetPath,
            message.BroadcastType,
            message.Theme,
            message.Dismissable,
            message.Active);
    }

    // ---- backlog-split/admin/part-5.json below ----

    // --- Compliance settings ---

    public static ComplianceSettingsSummary ToSummary(GitLabCompliancePolicySettings settings)
    {
        return new ComplianceSettingsSummary(
            settings.CspNamespaceId);
    }
}