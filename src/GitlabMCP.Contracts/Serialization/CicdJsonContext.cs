using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Cicd;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every "cicd" domain payload record (pipelines, jobs, variables,
///     triggers, job-token allowlist, CI lint). A separate context from <see cref="GitlabMcpJsonContext" /> —
///     see that file's header for why: splitting a single context's <c>[JsonSerializable]</c> attributes
///     across two files throws <c>CS8785</c> at build time on this SDK, so every domain gets its own context
///     class/file instead of appending to a shared one.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PipelineSummary))]
[JsonSerializable(typeof(PipelineListResult))]
[JsonSerializable(typeof(PipelineScheduleSummary))]
[JsonSerializable(typeof(PipelineScheduleListResult))]
[JsonSerializable(typeof(PipelineDeleteResult))]
[JsonSerializable(typeof(PipelineScheduleDeleteResult))]
[JsonSerializable(typeof(PipelineScheduleRunResult))]
[JsonSerializable(typeof(JobSummary))]
[JsonSerializable(typeof(JobListResult))]
[JsonSerializable(typeof(JobArtifactEntrySummary))]
[JsonSerializable(typeof(JobArtifactEntryListResult))]
[JsonSerializable(typeof(DeleteAllArtifactsResult))]
[JsonSerializable(typeof(JobArtifactArchiveDownload))]
[JsonSerializable(typeof(JobArtifactFileDownload))]
[JsonSerializable(typeof(JobArtifactsDeleteResult))]
[JsonSerializable(typeof(VariableSummary))]
[JsonSerializable(typeof(VariableListResult))]
[JsonSerializable(typeof(VariableDeleteResult))]
[JsonSerializable(typeof(TriggerSummary))]
[JsonSerializable(typeof(TriggerListResult))]
[JsonSerializable(typeof(TriggerCreateResult))]
[JsonSerializable(typeof(TriggerDeleteResult))]
[JsonSerializable(typeof(AllowlistEntrySummary))]
[JsonSerializable(typeof(JobTokenAllowlistResult))]
[JsonSerializable(typeof(JobTokenScopeSummary))]
[JsonSerializable(typeof(JobTokenScopeUpdateResult))]
[JsonSerializable(typeof(AllowlistEntryDeleteResult))]
[JsonSerializable(typeof(CiCatalogPublishResult))]
[JsonSerializable(typeof(CiLintIncludeSummary))]
[JsonSerializable(typeof(CiLintResultSummary))]
[JsonSerializable(typeof(TestReportTotalSummary))]
[JsonSerializable(typeof(TestSuiteSummary))]
[JsonSerializable(typeof(TestCaseSummary))]
[JsonSerializable(typeof(TestReportSummaryResult))]
public sealed partial class CicdJsonContext : JsonSerializerContext;