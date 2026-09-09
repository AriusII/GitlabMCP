using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Admin;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for the "admin" domain (instance administration, webhooks,
///     integrations, audit events, feature flags, license policies, background migrations). Its own
///     context, separate from <see cref="GitlabMcpJsonContext" /> and every other per-domain context --
///     splitting [JsonSerializable] attributes for the same partial class across two files throws CS8785 on
///     this SDK (see <c>GitlabMcpJsonContext.cs</c>'s own docstring). The host inserts
///     <see cref="AdminJsonContext.Default" /> into <c>GitLabJson.Options.TypeInfoResolverChain</c> alongside
///     the other per-domain contexts.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InstanceAppearanceSummary))]
[JsonSerializable(typeof(MetadataSummary))]
[JsonSerializable(typeof(InstanceStatisticsSummary))]
[JsonSerializable(typeof(InstanceSettingsSummary))]
[JsonSerializable(typeof(PlanLimitsSummary))]
[JsonSerializable(typeof(LicenseSummary))]
[JsonSerializable(typeof(LicenseListResult))]
[JsonSerializable(typeof(LicenseDeleteResult))]
[JsonSerializable(typeof(LicenseRefreshResult))]
[JsonSerializable(typeof(LicensePolicyDeleteResult))]
[JsonSerializable(typeof(SystemHookListResult))]
[JsonSerializable(typeof(FeatureFlagUserListSummary))]
[JsonSerializable(typeof(FeatureFlagUserListListResult))]
[JsonSerializable(typeof(FeatureFlagSummary))]
[JsonSerializable(typeof(LicensePolicySummary))]
[JsonSerializable(typeof(LicensePolicyListResult))]
[JsonSerializable(typeof(IntegrationSummary))]
[JsonSerializable(typeof(IntegrationListResult))]
[JsonSerializable(typeof(BackgroundMigrationOperationSummary))]
[JsonSerializable(typeof(BackgroundMigrationOperationListResult))]
[JsonSerializable(typeof(AuditEventSummary))]
[JsonSerializable(typeof(AuditEventListResult))]
[JsonSerializable(typeof(HookDeliverySummary))]
[JsonSerializable(typeof(HookDeliveryListResult))]
[JsonSerializable(typeof(HookSummary))]
[JsonSerializable(typeof(ProjectHookListResult))]
[JsonSerializable(typeof(GroupHookListResult))]
[JsonSerializable(typeof(HookDeleteResult))]
[JsonSerializable(typeof(HookTestResult))]
[JsonSerializable(typeof(HookDeliveryResendResult))]
// backlog-split/admin/part-3.json
[JsonSerializable(typeof(FeatureGateSummary))]
[JsonSerializable(typeof(InstanceFeatureSummary))]
[JsonSerializable(typeof(InstanceFeatureListResult))]
[JsonSerializable(typeof(InstanceFeatureDefinitionSummary))]
[JsonSerializable(typeof(InstanceFeatureDefinitionListResult))]
[JsonSerializable(typeof(InstanceFeatureGateDeleteResult))]
[JsonSerializable(typeof(FeatureFlagListResult))]
[JsonSerializable(typeof(FeatureFlagSettingsSummary))]
[JsonSerializable(typeof(IntegrationDisableResult))]
[JsonSerializable(typeof(PlatformTokenExchangeResult))]
[JsonSerializable(typeof(CiJobAllowedAgentsSummary))]
// backlog-split/admin/part-4.json
[JsonSerializable(typeof(FeatureFlagUserListDeleteResult))]
[JsonSerializable(typeof(OAuthApplicationSummary))]
[JsonSerializable(typeof(OAuthApplicationListResult))]
[JsonSerializable(typeof(OAuthApplicationWithSecretSummary))]
[JsonSerializable(typeof(OAuthApplicationDeleteResult))]
[JsonSerializable(typeof(BroadcastMessageSummary))]
[JsonSerializable(typeof(BroadcastMessageListResult))]
[JsonSerializable(typeof(SidekiqQueueJobDeleteResult))]
// backlog-split/admin/part-5.json
[JsonSerializable(typeof(BackgroundMigrationSummary))]
[JsonSerializable(typeof(BackgroundMigrationListResult))]
[JsonSerializable(typeof(UsageEventsTrackResult))]
[JsonSerializable(typeof(MigrationMarkAppliedResult))]
[JsonSerializable(typeof(ComplianceSettingsSummary))]
[JsonSerializable(typeof(ComplianceControlStatusResult))]
public sealed partial class AdminJsonContext : JsonSerializerContext;