using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Packages;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every Packages-domain payload record (parameter, return, or
///     <c>GitLabContent.Wrap&lt;T&gt;</c> payload type) crossing the MCP boundary.
///     ALL <c>[JsonSerializable]</c> attributes for this domain live in this ONE file/class, deliberately
///     separate from <see cref="GitlabMcpJsonContext" /> and every other domain's context — see that file's
///     header comment for the verified CS8785 generator bug that splitting a context's attribute-bearing
///     files triggers. Each domain gets its own context class instead.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PackageSummary))]
[JsonSerializable(typeof(PackageListResult))]
[JsonSerializable(typeof(PackageDeleteResult))]
[JsonSerializable(typeof(PackageDetail))]
[JsonSerializable(typeof(ContainerRepositorySummary))]
[JsonSerializable(typeof(ContainerRepositoryListResult))]
[JsonSerializable(typeof(ContainerRepositoryDeleteResult))]
[JsonSerializable(typeof(ContainerRepositoryTagSummary))]
[JsonSerializable(typeof(ContainerRepositoryTagListResult))]
[JsonSerializable(typeof(ContainerRepositoryTagDetail))]
[JsonSerializable(typeof(ContainerRepositoryTagDeleteResult))]
[JsonSerializable(typeof(ContainerRepositoryTagsBulkDeleteResult))]
[JsonSerializable(typeof(DebianDistributionSummary))]
[JsonSerializable(typeof(DebianDistributionListResult))]
[JsonSerializable(typeof(DebianDistributionDeleteResult))]
[JsonSerializable(typeof(TerraformModuleVersionSummary))]
[JsonSerializable(typeof(TerraformModuleEntrySummary))]
[JsonSerializable(typeof(TerraformModuleVersionListResult))]
[JsonSerializable(typeof(TerraformModuleSummary))]
[JsonSerializable(typeof(PackageProtectionRuleSummary))]
[JsonSerializable(typeof(PackageProtectionRuleListResult))]
[JsonSerializable(typeof(PackageProtectionRuleDeleteResult))]
[JsonSerializable(typeof(ContainerRegistryProtectionRuleSummary))]
[JsonSerializable(typeof(ContainerRegistryProtectionRuleListResult))]
[JsonSerializable(typeof(ContainerRegistryProtectionRuleDeleteResult))]
[JsonSerializable(typeof(ContainerRegistryProtectionTagRuleSummary))]
[JsonSerializable(typeof(ContainerRegistryProtectionTagRuleListResult))]
[JsonSerializable(typeof(ContainerRegistryProtectionTagRuleDeleteResult))]
[JsonSerializable(typeof(PackageFileSummary))]
[JsonSerializable(typeof(PackageFileListResult))]
[JsonSerializable(typeof(PackageFileDeleteResult))]
[JsonSerializable(typeof(GenericPackageFileDownload))]
[JsonSerializable(typeof(PackagePipelineSummary))]
[JsonSerializable(typeof(PackagePipelineListResult))]
[JsonSerializable(typeof(DependencyProxyCachePurgeResult))]
public sealed partial class PackagesJsonContext : JsonSerializerContext;