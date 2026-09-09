namespace GitlabMCP.Contracts.Admin;

/// <summary>
///     Projection records for the "admin" domain (instance administration, webhooks, integrations, audit,
///     feature flags, license policies, background migrations). Every string here can originate from GitLab
///     (branding text, hook URLs, integration titles, audit event names, …) so every tool returning these
///     wraps via <see cref="GitLabContent" />.
///     Deliberately never projected, per CLAUDE.md rule 5: <c>GitLabIntegration.Properties</c> (a raw
///     property bag that legitimately holds third-party credentials -- Jira passwords, Slack webhook
///     tokens, Datadog API keys), <c>GitLabAuditEvent.Details</c> (an unbounded raw property bag whose
///     shape GitLab does not document per event type), and <c>GitLabHookUrlVariable.Value</c> /
///     <c>GitLabHookCustomHeader.Value</c> (GitLab itself never echoes these back non-null, but the member
///     is skipped by construction rather than trusted to stay null).
/// </summary>

// --- Instance appearance (IInstanceClient) ---
public sealed record InstanceAppearanceSummary(
    string? Title,
    string? Description,
    string? PwaName,
    string? PwaShortName,
    string? PwaDescription,
    string? LogoUrl,
    string? PwaIconUrl,
    string? HeaderLogoUrl,
    string? FaviconUrl,
    string? NewProjectGuidelines,
    string? MemberGuidelines,
    string? ProfileImageGuidelines,
    string? HeaderMessage,
    string? FooterMessage,
    string? MessageBackgroundColor,
    string? MessageFontColor,
    bool? EmailHeaderAndFooterEnabled,
    string? SiteName);

// --- Instance metadata, statistics and settings (IInstanceClient) ---

/// <summary><c>Kas*</c> fields are the nested GitLab Agent Server for Kubernetes status.</summary>
public sealed record MetadataSummary(
    string? Version,
    string? Revision,
    bool? Enterprise,
    bool? KasEnabled,
    string? KasVersion,
    string? KasExternalUrl);

/// <summary>Instance-wide entity counts. Every field is a server-computed count -- never wrapped.</summary>
public sealed record InstanceStatisticsSummary(
    int? Forks,
    int? Issues,
    int? MergeRequests,
    int? Notes,
    int? Snippets,
    int? SshKeys,
    int? Milestones,
    int? Users,
    int? Projects,
    int? Groups,
    int? ActiveUsers);

/// <summary>
///     The instance-wide application settings (<c>GET</c>/<c>PUT /application/settings</c>). GitLab's own
///     OpenAPI spec types nearly every member of this response as a bare string, including fields that are
///     booleans or integers on the write side -- <c>GitLab.Client.Models.GitLabApplicationSettings</c>'s own
///     docstring explains why, so every field here is <c>string?</c> to match. Every field the model exposes
///     is included <b>except</b> two secret-shaped ones: the Akismet API key and the static-objects
///     external-storage auth token (rule 5 -- neither is ever projected).
/// </summary>
public sealed record InstanceSettingsSummary(
    string? Id,
    string? PerformanceBarAllowedGroupId,
    string? AbuseNotificationEmail,
    string? AdminMode,
    string? AfterSignOutPath,
    string? AfterSignUpText,
    string? AkismetEnabled,
    string? AllowLocalRequestsFromWebHooksAndServices,
    string? AllowLocalRequestsFromSystemHooks,
    string? AllowPossibleSpam,
    string? DnsRebindingProtectionEnabled,
    string? ArchiveBuildsInHumanReadable,
    string? AssetProxyEnabled,
    string? AssetProxyUrl,
    string? AssetProxyAllowlist,
    string? StaticObjectsExternalStorageUrl,
    string? AuthnDataRetentionCleanupEnabled,
    string? AuthorizedKeysEnabled,
    string? AutoDevopsEnabled,
    string? AutoDevopsDomain,
    string? AutocompleteUsersLimit,
    string? AutocompleteUsersUnauthenticatedLimit,
    string? AllowBypassPlaceholderConfirmation,
    string? CiDeletePipelinesInSecondsLimitHumanReadable,
    string? CiJobLiveTraceEnabled,
    string? CiPartitionsInSecondsLimitHumanReadable,
    string? ConcurrentGithubImportJobsLimit,
    string? ConcurrentBitbucketImportJobsLimit,
    string? ConcurrentBitbucketServerImportJobsLimit,
    string? ConcurrentPullRequestImportJobsLimit,
    string? ImportJobsConcurrencyLimit,
    string? ContainerExpirationPoliciesEnableHistoricEntries,
    string? ContainerRegistryExpirationPoliciesCaching,
    string? ContainerRegistryTokenExpireDelay,
    string? OauthAccessTokenExpiresIn,
    string? DecompressArchiveFileTimeout,
    string? DefaultArtifactsExpireIn,
    string? DefaultBranchName,
    string? DefaultBranchProtection,
    string? DefaultBranchProtectionDefaults,
    string? DefaultCiConfigPath,
    string? DefaultGroupVisibility,
    string? DefaultPreferredLanguage,
    string? DefaultProjectCreation,
    string? DefaultProjectVisibility,
    string? DefaultProjectsLimit,
    string? DefaultSnippetVisibility,
    string? DefaultSyntaxHighlightingTheme);

/// <summary>
///     A billing plan's resource limits (<c>GET</c>/<c>PUT /application/plan_limits</c>). Every field is a
///     server-computed count or byte size -- never wrapped. <c>LimitsHistory</c> (a per-limit change log) is
///     deliberately not projected: it is a nested, unbounded structure outside this tool surface's scope.
/// </summary>
public sealed record PlanLimitsSummary(
    long? CargoMaxFileSize,
    long? CiInstanceLevelVariables,
    long? CiPipelineSize,
    long? CiActiveJobs,
    long? CiProjectSubscriptions,
    long? CiPipelineSchedules,
    long? CiNeedsSizeLimit,
    long? CiRegisteredGroupRunners,
    long? CiRegisteredProjectRunners,
    long? ConanMaxFileSize,
    long? DotenvVariables,
    long? DotenvSize,
    long? EnforcementLimit,
    long? GenericPackagesMaxFileSize,
    long? HelmMaxFileSize,
    long? MavenMaxFileSize,
    long? NotificationLimit,
    long? NpmMaxFileSize,
    long? NugetMaxFileSize,
    long? PipelineHierarchySize,
    long? PypiMaxFileSize,
    long? ServiceDeskOutboundEmailsPerHour,
    long? ServiceDeskOutboundEmailsPerDay,
    long? TerraformModuleMaxFileSize,
    long? StorageSizeLimit,
    long? WebHookCalls,
    long? WebHookCallsLow,
    long? WebHookCallsMid,
    long? MaxPipelinesPerMergeTrain);

// --- Licenses (ILicensesClient) ---

/// <summary>
///     <see cref="LicenseeName" />/<see cref="LicenseeCompany" /> are extracted from GitLab's untyped
///     <c>Licensee</c> object; the licensee's contact email is deliberately not projected. The license key
///     itself is never returned by GitLab and therefore never appears here. <see cref="ActiveUsers" /> is
///     null on entries that came from <c>gitlab_list_licenses</c> -- GitLab's listing endpoint omits it.
/// </summary>
public sealed record LicenseSummary(
    long Id,
    string? Plan,
    DateTimeOffset? CreatedAt,
    DateOnly? StartsAt,
    DateOnly? ExpiresAt,
    int? HistoricalMax,
    int? MaximumUserCount,
    string? LicenseeName,
    string? LicenseeCompany,
    bool? Expired,
    int? Overage,
    int? UserLimit,
    int? ActiveUsers);

public sealed record LicenseListResult(IReadOnlyList<LicenseSummary> Licenses, bool Truncated);

/// <summary>All-scalar confirmation of a delete -- never wrapped.</summary>
public sealed record LicenseDeleteResult(bool Deleted);

/// <summary>All-scalar result of a billable-user recalculation -- never wrapped.</summary>
public sealed record LicenseRefreshResult(bool Success);

/// <summary>All-scalar confirmation of a delete -- never wrapped.</summary>
public sealed record LicensePolicyDeleteResult(bool Deleted);

// --- System hooks (ISystemHooksClient) ---

public sealed record SystemHookListResult(IReadOnlyList<HookSummary> Hooks, bool Truncated);

// --- Feature flags (IFeatureFlagsClient) ---

public sealed record FeatureFlagUserListSummary(
    long Id,
    long Iid,
    string? Name,
    string? UserXids,
    long? ProjectId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record FeatureFlagUserListListResult(IReadOnlyList<FeatureFlagUserListSummary> UserLists, bool Truncated);

public sealed record FeatureFlagSummary(
    string? Name,
    string? Description,
    bool? Active,
    string? Version,
    IReadOnlyList<string> StrategyNames,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

// --- License policies (ILicensesClient) ---

public sealed record LicensePolicySummary(long Id, string? Name, string? ApprovalStatus);

public sealed record LicensePolicyListResult(IReadOnlyList<LicensePolicySummary> Policies, bool Truncated);

// --- Integrations (IIntegrationsClient) ---

public sealed record IntegrationSummary(
    long Id,
    string? Title,
    string? Slug,
    bool? Active,
    bool? Inherited,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record IntegrationListResult(IReadOnlyList<IntegrationSummary> Integrations, bool Truncated);

// --- Background migrations (IBackgroundMigrationsClient) ---

public sealed record BackgroundMigrationOperationSummary(
    string? Id,
    int? Partition,
    string? JobClassName,
    string? TableName,
    string? ColumnName,
    string? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset? OnHoldUntil);

public sealed record BackgroundMigrationOperationListResult(
    IReadOnlyList<BackgroundMigrationOperationSummary> Operations,
    bool Truncated);

public sealed record BackgroundMigrationSummary(
    string? Id,
    string? JobClassName,
    string? TableName,
    string? ColumnName,
    string? Status,
    double? Progress,
    DateTimeOffset? CreatedAt,
    string? EstimatedTimeRemaining);

public sealed record BackgroundMigrationListResult(
    IReadOnlyList<BackgroundMigrationSummary> Migrations,
    bool Truncated);

// --- Audit events (IAuditEventsClient) ---

/// <summary>
///     <c>Details</c> (a raw, per-event-type property bag) is deliberately not projected -- see this file's
///     header comment.
/// </summary>
public sealed record AuditEventSummary(
    long Id,
    long? AuthorId,
    long? EntityId,
    string? EntityType,
    string? EventName,
    DateTimeOffset? CreatedAt);

public sealed record AuditEventListResult(IReadOnlyList<AuditEventSummary> Events, bool Truncated);

// --- Webhook delivery logs (IProjectHooksClient / IGroupHooksClient) ---

/// <summary>
///     One webhook delivery attempt. <c>RequestHeaders</c>/<c>RequestData</c>/<c>ResponseHeaders</c> are not
///     projected: headers can carry a caller-configured secret verbatim, and the request payload duplicates
///     whatever GitLab event triggered it. <see cref="ResponseBodyPreview" /> is capped at 500 characters.
/// </summary>
public sealed record HookDeliverySummary(
    long Id,
    string? Url,
    string? Trigger,
    string? ResponseStatus,
    string? ResponseBodyPreview,
    bool ResponseBodyTruncated,
    double? ExecutionDurationSeconds,
    string? InternalErrorMessage);

public sealed record HookDeliveryListResult(IReadOnlyList<HookDeliverySummary> Deliveries, bool Truncated);

// --- Webhooks (IProjectHooksClient / IGroupHooksClient / ISystemHooksClient) ---

/// <summary>
///     A created/registered webhook (project, group or system scope -- the scope is implied by the tool and
///     carried in the envelope's <c>source</c>, not repeated here). <c>TokenPresent</c>/<c>SigningTokenPresent</c>
///     report only whether a secret was configured; GitLab never echoes the secret itself. Likewise
///     <see cref="UrlVariableKeys" />/<see cref="CustomHeaderKeys" /> carry only the configured names -- GitLab
///     itself never returns the values (see this file's header comment).
/// </summary>
public sealed record HookSummary(
    long Id,
    string? Url,
    string? Name,
    string? Description,
    IReadOnlyList<string> EnabledEvents,
    bool? TokenPresent,
    bool? SigningTokenPresent,
    IReadOnlyList<string> UrlVariableKeys,
    IReadOnlyList<string> CustomHeaderKeys,
    string? AlertStatus,
    DateTimeOffset? CreatedAt);

public sealed record ProjectHookListResult(IReadOnlyList<HookSummary> Hooks, bool Truncated);

public sealed record GroupHookListResult(IReadOnlyList<HookSummary> Hooks, bool Truncated);

/// <summary>All-scalar confirmation of a webhook delete -- never wrapped.</summary>
public sealed record HookDeleteResult(bool Deleted);

/// <summary>All-scalar confirmation that a webhook test delivery was fired -- never wrapped.</summary>
public sealed record HookTestResult(bool Triggered);

/// <summary>All-scalar confirmation that a webhook delivery was resent -- never wrapped.</summary>
public sealed record HookDeliveryResendResult(bool Resent);

// ---- backlog-split/admin/part-3.json below ----

// --- Instance features (IFeaturesClient) -- GitLab's own Flipper development flags, distinct from a
// project's feature flags (IFeatureFlagsClient, below). Reading and writing them requires instance
// administrator access. ---

/// <summary>
///     One gate on an instance feature. <see cref="Value" /> is the gate's raw value rendered as text --
///     depending on <see cref="Key" /> it may be a bool, an integer percentage, or a comma-separated list of
///     qualifying actor ids -- via <c>JsonElement.ToString()</c>, which returns a bare string for a JSON
///     string value and the raw JSON text for every other kind.
/// </summary>
public sealed record FeatureGateSummary(string? Key, string? Value);

public sealed record InstanceFeatureSummary(
    string? Name,
    string? State,
    IReadOnlyList<FeatureGateSummary> Gates,
    string? DefinitionType,
    string? DefinitionGroup,
    string? DefinitionMilestone,
    bool? DefinitionDefaultEnabled);

public sealed record InstanceFeatureListResult(IReadOnlyList<InstanceFeatureSummary> Features, bool Truncated);

public sealed record InstanceFeatureDefinitionSummary(
    string? Name,
    string? Type,
    string? Group,
    string? Milestone,
    string? IntendedToRolloutBy,
    bool? DefaultEnabled,
    bool? LogStateChanges,
    string? FeatureIssueUrl,
    string? IntroducedByUrl,
    string? RolloutIssueUrl);

public sealed record InstanceFeatureDefinitionListResult(
    IReadOnlyList<InstanceFeatureDefinitionSummary> Definitions,
    bool Truncated);

/// <summary>All-scalar confirmation of an instance feature gate delete -- never wrapped.</summary>
public sealed record InstanceFeatureGateDeleteResult(bool Deleted);

// --- Project feature flags (IFeatureFlagsClient) ---

public sealed record FeatureFlagListResult(IReadOnlyList<FeatureFlagSummary> Flags, bool Truncated);

public sealed record FeatureFlagSettingsSummary(string? MinimumRole);

// --- Integrations disable (IIntegrationsClient) ---

/// <summary>All-scalar confirmation of an integration disable -- never wrapped.</summary>
public sealed record IntegrationDisableResult(bool Disabled);

// --- Platform integrations (IPlatformIntegrationsClient) ---

/// <summary>
///     The JWT issued by <c>gitlab_exchange_platform_token</c>. <see cref="Token" /> is a bearer credential
///     for the target modular service -- projected anyway under the same plaintext-credential carve-out
///     PeopleTools.cs documents for personal/impersonation/resource access token creation: this tool's
///     entire purpose is minting and returning it once. Never logged or echoed by any other tool.
/// </summary>
public sealed record PlatformTokenExchangeResult(string Token);

/// <summary>
///     Identity of the CI job the calling job token belongs to. GitLab's own OpenAPI spec documents this
///     operation's response as the CI job entity rather than an agent list (see
///     <c>IPlatformIntegrationsClient.GetAllowedAgentsAsync</c>'s own remarks), so this projects only the
///     job's identifying fields rather than the full <c>GitLabJob</c> shape.
/// </summary>
public sealed record CiJobAllowedAgentsSummary(
    long Id,
    string? Name,
    string? Status,
    string? Stage,
    string? Ref,
    string? WebUrl);

// ---- backlog-split/admin/part-4.json below ----

// --- Feature flag user list writes (IFeatureFlagsClient) ---

/// <summary>All-scalar confirmation of a feature flag user list delete -- never wrapped.</summary>
public sealed record FeatureFlagUserListDeleteResult(bool Deleted);

// --- OAuth applications (IApplicationsClient) ---

/// <summary>
///     A registered OAuth application, instance-wide or per-user depending on which tool returned it.
///     <c>GitLabApplication</c> (the type this is projected from) deliberately carries no secret member --
///     GitLab discloses the client secret only from the create/renew operations, projected separately as
///     <see cref="OAuthApplicationWithSecretSummary" />.
/// </summary>
public sealed record OAuthApplicationSummary(
    long Id,
    string? ApplicationId,
    string? ApplicationName,
    string? CallbackUrl,
    bool? Confidential,
    IReadOnlyList<string> Scopes);

public sealed record OAuthApplicationListResult(IReadOnlyList<OAuthApplicationSummary> Applications, bool Truncated);

/// <summary>
///     A newly created OAuth application together with its plaintext client secret -- projected anyway
///     under the same plaintext-credential carve-out PeopleTools.cs documents for personal/impersonation/
///     resource access token creation and AdminTools.cs's own <c>ExchangePlatformTokenAsync</c> already
///     uses: minting and returning this secret exactly once is the entire purpose of
///     <c>gitlab_create_oauth_application</c>/<c>gitlab_create_my_oauth_application</c>. GitLab cannot
///     disclose it again afterward, and no other tool ever echoes it.
/// </summary>
public sealed record OAuthApplicationWithSecretSummary(
    long Id,
    string? ApplicationId,
    string? ApplicationName,
    string? CallbackUrl,
    bool? Confidential,
    IReadOnlyList<string> Scopes,
    string Secret);

/// <summary>All-scalar confirmation of an OAuth application delete -- never wrapped.</summary>
public sealed record OAuthApplicationDeleteResult(bool Deleted);

// --- Broadcast messages (IBroadcastMessagesClient) ---

public sealed record BroadcastMessageSummary(
    long Id,
    string? Message,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? Color,
    string? Font,
    IReadOnlyList<int> TargetAccessLevels,
    string? TargetPath,
    string? BroadcastType,
    string? Theme,
    bool? Dismissable,
    bool? Active);

public sealed record BroadcastMessageListResult(IReadOnlyList<BroadcastMessageSummary> Messages, bool Truncated);

// --- Sidekiq admin (ISidekiqClient) ---

/// <summary>All-scalar confirmation of a Sidekiq queue job delete -- never wrapped.</summary>
public sealed record SidekiqQueueJobDeleteResult(bool Deleted);

// ---- backlog-split/admin/part-5.json below ----

// --- Usage data / internal event tracking (IUsageDataClient) ---

/// <summary>All-scalar confirmation of a usage-event tracking call -- never wrapped.</summary>
public sealed record UsageEventsTrackResult(int Tracked);

// --- Admin migrations (IAdminMigrationsClient) ---

/// <summary>All-scalar confirmation that a pending migration was marked applied -- never wrapped.</summary>
public sealed record MigrationMarkAppliedResult(bool Applied);

// --- Compliance settings (IComplianceSettingsClient) ---

/// <summary>
///     The instance's centralized security policies (CSP) namespace setting. The sole field is a
///     server-computed id -- never wrapped.
/// </summary>
public sealed record ComplianceSettingsSummary(long? CspNamespaceId);

/// <summary>All-scalar confirmation that a compliance control status was reported -- never wrapped.</summary>
public sealed record ComplianceControlStatusResult(bool Reported);