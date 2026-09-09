using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Infra;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for the "infra" domain (Terraform state protection, cluster-agent
///     tokens, dependency scanning, error tracking, attestations). Its own context, separate from
///     <see cref="GitlabMcpJsonContext" /> and every other per-domain context — splitting [JsonSerializable]
///     attributes for the same partial class across two files throws CS8785 on this SDK (see
///     <c>GitlabMcpJsonContext.cs</c>'s own docstring). The host inserts <see cref="InfraJsonContext.Default" />
///     into <c>GitLabJson.Options.TypeInfoResolverChain</c> alongside the other per-domain contexts.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DependencyVulnerabilitySummary))]
[JsonSerializable(typeof(DependencyLicenseSummary))]
[JsonSerializable(typeof(DependencySummary))]
[JsonSerializable(typeof(DependencyListResult))]
[JsonSerializable(typeof(ClusterAgentTokenSummary))]
[JsonSerializable(typeof(ClusterAgentTokenListResult))]
[JsonSerializable(typeof(ClusterAgentTokenCreated))]
[JsonSerializable(typeof(TerraformStateProtectionRuleSummary))]
[JsonSerializable(typeof(TerraformStateProtectionRuleListResult))]
[JsonSerializable(typeof(ErrorTrackingClientKeySummary))]
[JsonSerializable(typeof(ErrorTrackingClientKeyListResult))]
[JsonSerializable(typeof(AttestationBundle))]
[JsonSerializable(typeof(TerraformStateDocument))]
[JsonSerializable(typeof(TerraformStateUploadResult))]
[JsonSerializable(typeof(TerraformStateDeleteResult))]
[JsonSerializable(typeof(TerraformStateVersionDeleteResult))]
[JsonSerializable(typeof(TerraformStateLockResult))]
[JsonSerializable(typeof(TerraformStateUnlockResult))]
[JsonSerializable(typeof(TerraformStateProtectionRuleDeleteResult))]
[JsonSerializable(typeof(ClusterAgentConfigProjectSummary))]
[JsonSerializable(typeof(ClusterAgentSummary))]
[JsonSerializable(typeof(ClusterAgentListResult))]
[JsonSerializable(typeof(ClusterAgentDeleteResult))]
[JsonSerializable(typeof(ClusterAgentTokenRevokeResult))]
[JsonSerializable(typeof(ClusterAgentUrlConfigurationSummary))]
[JsonSerializable(typeof(ClusterAgentUrlConfigurationResult))]
[JsonSerializable(typeof(ClusterAgentUrlConfigurationDeleteResult))]
[JsonSerializable(typeof(DependencyExportSummary))]
[JsonSerializable(typeof(DependencyExportFile))]
[JsonSerializable(typeof(DependencyVulnerabilityListResult))]
[JsonSerializable(typeof(AttestationSummary))]
[JsonSerializable(typeof(AttestationListResult))]
[JsonSerializable(typeof(SbomScanResult))]
[JsonSerializable(typeof(ErrorTrackingSettingsSummary))]
[JsonSerializable(typeof(AlertMetricImageSummary))]
[JsonSerializable(typeof(AlertMetricImageListResult))]
[JsonSerializable(typeof(AlertMetricImageDeleteResult))]
public sealed partial class InfraJsonContext : JsonSerializerContext;