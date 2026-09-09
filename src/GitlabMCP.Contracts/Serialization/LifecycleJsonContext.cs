using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Lifecycle;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every Lifecycle (import/export, ML, Duo Chat) payload record. Kept
///     as its own context/file per CLAUDE.md's per-domain instructions: a JsonSerializerContext's
///     [JsonSerializable] attributes must all live in ONE file (splitting across files throws CS8785 on this
///     SDK — see GitlabMcpJsonContext.cs), and this domain's records must not be appended to that shared
///     Epics/Ping context, to keep each domain's build independent of the others. <c>gitlab_duo_chat</c>
///     needs no entry here — it returns raw text via <see cref="GitLabContent.WrapText" />, never a typed
///     payload.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProjectTemplateSummary))]
[JsonSerializable(typeof(ProjectTemplateListResult))]
[JsonSerializable(typeof(BulkImportEntitySummary))]
[JsonSerializable(typeof(BulkImportEntityListResult))]
[JsonSerializable(typeof(BulkImportSummary))]
[JsonSerializable(typeof(MlExperimentTag))]
[JsonSerializable(typeof(MlExperimentSummary))]
[JsonSerializable(typeof(MlExperimentListResult))]
[JsonSerializable(typeof(MlExperimentCreateResult))]
[JsonSerializable(typeof(GroupExportDownloadInfo))]
[JsonSerializable(typeof(ProjectExportScheduleResult))]
[JsonSerializable(typeof(ProjectExportStatusSummary))]
[JsonSerializable(typeof(ProjectImportStatusSummary))]
[JsonSerializable(typeof(ImportedProjectSummary))]
[JsonSerializable(typeof(ProjectTemplateDetail))]
[JsonSerializable(typeof(GroupExportScheduleResult))]
[JsonSerializable(typeof(GroupRelationsExportStatusSummary))]
[JsonSerializable(typeof(GroupRelationsExportStatusListResult))]
[JsonSerializable(typeof(BulkImportListResult))]
[JsonSerializable(typeof(BulkImportEntityFailureSummary))]
[JsonSerializable(typeof(BulkImportEntityFailureListResult))]
[JsonSerializable(typeof(GitHubGistsImportScheduleResult))]
[JsonSerializable(typeof(MlRunMetric))]
[JsonSerializable(typeof(MlRunSummary))]
[JsonSerializable(typeof(MlRunSearchResult))]
[JsonSerializable(typeof(MlRunLogBatchResult))]
[JsonSerializable(typeof(MlModelSummary))]
[JsonSerializable(typeof(MlModelListResult))]
[JsonSerializable(typeof(MlModelVersionSummary))]
[JsonSerializable(typeof(GlqlFieldSummary))]
[JsonSerializable(typeof(GlqlQueryResult))]
[JsonSerializable(typeof(CodeSuggestionsEnabledResult))]
[JsonSerializable(typeof(AgentIdentitySummary))]
[JsonSerializable(typeof(AgentSessionSummary))]
[JsonSerializable(typeof(AgentSessionListResult))]
[JsonSerializable(typeof(ExperimentSummary))]
[JsonSerializable(typeof(ExperimentListResult))]
[JsonSerializable(typeof(ExperimentAssignmentSummary))]
[JsonSerializable(typeof(AdminModelChecksumInfo))]
[JsonSerializable(typeof(AdminModelRecordSummary))]
[JsonSerializable(typeof(AdminModelRecordListResult))]
[JsonSerializable(typeof(DictionaryTableSummary))]
[JsonSerializable(typeof(DictionaryTableListResult))]
[JsonSerializable(typeof(RolloutSummary))]
[JsonSerializable(typeof(JiraForgeSubscriptionSummary))]
[JsonSerializable(typeof(JiraForgeSubscriptionListResult))]
[JsonSerializable(typeof(JiraForgeMutationResult))]
[JsonSerializable(typeof(MobilePushSubscriptionSummary))]
[JsonSerializable(typeof(MobilePushSubscriptionUnregisterResult))]
public sealed partial class LifecycleJsonContext : JsonSerializerContext;