using System.Text.Json.Serialization;
using GitlabMCP.Contracts.MergeRequests;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every merge-requests-domain payload record crossing the MCP
///     boundary. ALL [JsonSerializable] attributes for this context live in THIS ONE FILE — splitting them
///     across multiple files of the same partial class throws CS8785 at build time on this SDK (see
///     GitlabMcpJsonContext.cs for the verified repro). This domain gets its own context/class/file rather
///     than adding to GitlabMcpJsonContext, which belongs to Epics/Ping.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MergeRequestSummary))]
[JsonSerializable(typeof(MergeRequestListResult))]
[JsonSerializable(typeof(MergeRequestResult))]
[JsonSerializable(typeof(CommitSummary))]
[JsonSerializable(typeof(MergeRequestCommitListResult))]
[JsonSerializable(typeof(PipelineSummary))]
[JsonSerializable(typeof(MergeRequestPipelineListResult))]
[JsonSerializable(typeof(IssueRefSummary))]
[JsonSerializable(typeof(MergeRequestIssueListResult))]
[JsonSerializable(typeof(ReviewerSummary))]
[JsonSerializable(typeof(MergeRequestReviewerListResult))]
[JsonSerializable(typeof(ApprovalConfigurationResult))]
[JsonSerializable(typeof(ApprovalRuleSummary))]
[JsonSerializable(typeof(ApprovalRuleListResult))]
[JsonSerializable(typeof(MergeRequestApprovalRuleState))]
[JsonSerializable(typeof(MergeRequestApprovalsResult))]
[JsonSerializable(typeof(ApprovalActionResult))]
[JsonSerializable(typeof(MergeTrainCarSummary))]
[JsonSerializable(typeof(MergeTrainListResult))]
[JsonSerializable(typeof(ExternalStatusCheckSummary))]
[JsonSerializable(typeof(ExternalStatusCheckListResult))]
[JsonSerializable(typeof(MergeRequestDeleteResult))]
[JsonSerializable(typeof(MergeRequestRebaseResult))]
[JsonSerializable(typeof(DiffFileSummary))]
[JsonSerializable(typeof(MergeRequestDiffListResult))]
[JsonSerializable(typeof(ApprovalRuleDeleteResult))]
public sealed partial class MergeRequestsJsonContext : JsonSerializerContext;