using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Search;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for the "search" domain (search/analytics/wiki/snippets/templates,
///     plus the Zoekt/Knowledge-Graph/ActiveContext admin surfaces). Its own context, separate from
///     <see cref="GitlabMcpJsonContext" /> and every other domain's context — splitting [JsonSerializable]
///     attributes for the same partial class across two files throws CS8785 on this SDK (see
///     <c>GitlabMcpJsonContext.cs</c>'s own docstring); every domain that lands concurrently gets its own
///     context class instead. The host inserts <see cref="SearchJsonContext.Default" /> into
///     <c>GitLabJson.Options.TypeInfoResolverChain</c> alongside the other per-domain contexts.
///     Every payload/parameter type this domain's tools touch is listed explicitly, including
///     <see cref="SnippetFileInput" />'s own <c>IReadOnlyList&lt;&gt;</c> wrapper: a bare tool parameter type
///     is a *root* lookup against the resolver chain, not something reached by walking another registered
///     type's properties, so it needs its own entry even though its element type is also listed.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SnippetSummary))]
[JsonSerializable(typeof(SnippetListResult))]
[JsonSerializable(typeof(SnippetFileInput))]
[JsonSerializable(typeof(IReadOnlyList<SnippetFileInput>))]
[JsonSerializable(typeof(LicenseTemplateSummary))]
[JsonSerializable(typeof(LicenseTemplateListResult))]
[JsonSerializable(typeof(CodeReviewAnalyticsItemSummary))]
[JsonSerializable(typeof(CodeReviewAnalyticsListResult))]
[JsonSerializable(typeof(WikiPageSummary))]
[JsonSerializable(typeof(WikiPageListResult))]
[JsonSerializable(typeof(ZoektIndexedNamespaceSummary))]
[JsonSerializable(typeof(ZoektIndexedNamespaceListResult))]
[JsonSerializable(typeof(KnowledgeGraphNamespaceSummary))]
[JsonSerializable(typeof(KnowledgeGraphNamespaceListResult))]
[JsonSerializable(typeof(KnowledgeGraphDisableResult))]
[JsonSerializable(typeof(ActiveContextCollectionResult))]
[JsonSerializable(typeof(SearchProjectSummary))]
[JsonSerializable(typeof(SearchProjectListResult))]
[JsonSerializable(typeof(SearchIssueSummary))]
[JsonSerializable(typeof(SearchIssueListResult))]
[JsonSerializable(typeof(SearchMergeRequestSummary))]
[JsonSerializable(typeof(SearchMergeRequestListResult))]
[JsonSerializable(typeof(SearchUserSummary))]
[JsonSerializable(typeof(SearchUserListResult))]
[JsonSerializable(typeof(SearchMilestoneSummary))]
[JsonSerializable(typeof(SearchMilestoneListResult))]
[JsonSerializable(typeof(SearchNoteSummary))]
[JsonSerializable(typeof(SearchNoteListResult))]
[JsonSerializable(typeof(SearchCommitSummary))]
[JsonSerializable(typeof(SearchCommitListResult))]
[JsonSerializable(typeof(SemanticCodeSnippetRangeSummary))]
[JsonSerializable(typeof(SemanticCodeSearchMatchSummary))]
[JsonSerializable(typeof(SemanticCodeSearchSummary))]
[JsonSerializable(typeof(SearchMigrationSummary))]
[JsonSerializable(typeof(WikiPageDeleteResult))]
[JsonSerializable(typeof(WikiAttachmentSummary))]
[JsonSerializable(typeof(SnippetDeleteResult))]
[JsonSerializable(typeof(UpdateSnippetFileInput))]
[JsonSerializable(typeof(IReadOnlyList<UpdateSnippetFileInput>))]
[JsonSerializable(typeof(SnippetUserAgentDetailSummary))]
[JsonSerializable(typeof(TemplateCatalogEntry))]
[JsonSerializable(typeof(TemplateCatalogListResult))]
[JsonSerializable(typeof(TemplateDetail))]
[JsonSerializable(typeof(LicenseTemplateDetail))]
[JsonSerializable(typeof(GroupActivitySummaryResult))]
[JsonSerializable(typeof(DeploymentFrequencyPointSummary))]
[JsonSerializable(typeof(DeploymentFrequencyListResult))]
[JsonSerializable(typeof(DoraMetricPointSummary))]
[JsonSerializable(typeof(DoraMetricsResult))]
[JsonSerializable(typeof(ZoektNodeSummary))]
[JsonSerializable(typeof(ZoektNodeListResult))]
[JsonSerializable(typeof(ZoektIndexedNamespaceRemoveResult))]
[JsonSerializable(typeof(ZoektProjectIndexResult))]
[JsonSerializable(typeof(ActiveContextConnectionSummary))]
[JsonSerializable(typeof(ActiveContextConnectionListResult))]
[JsonSerializable(typeof(ActiveContextNamespaceStateResult))]
[JsonSerializable(typeof(ActiveContextDeadQueueClearResult))]
[JsonSerializable(typeof(ActiveContextDeadQueueReplayResult))]
public sealed partial class SearchJsonContext : JsonSerializerContext;