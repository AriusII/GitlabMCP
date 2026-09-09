using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Planning;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every Planning-domain payload record (parameter, return, or
///     <c>GitLabContent.Wrap&lt;T&gt;</c> payload type) crossing the MCP boundary.
///     ALL <c>[JsonSerializable]</c> attributes for this domain live in this ONE file/class, deliberately
///     separate from <see cref="GitlabMcpJsonContext" /> — see that file's header comment for the verified
///     CS8785 generator bug that splitting a context's attribute-bearing files triggers. Each domain gets its
///     own context class instead.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(IssueSummary))]
[JsonSerializable(typeof(IssueListResult))]
[JsonSerializable(typeof(IssueLinkSummary))]
[JsonSerializable(typeof(IssueLinkListResult))]
[JsonSerializable(typeof(IssueSubscriptionResult))]
[JsonSerializable(typeof(ParticipantSummary))]
[JsonSerializable(typeof(ParticipantListResult))]
[JsonSerializable(typeof(IssueDeleteResult))]
[JsonSerializable(typeof(IssueUnlinkResult))]
[JsonSerializable(typeof(TimeStatsSummary))]
[JsonSerializable(typeof(IssueStatisticsResult))]
[JsonSerializable(typeof(IssueLinkResult))]
[JsonSerializable(typeof(RelatedMergeRequestSummary))]
[JsonSerializable(typeof(RelatedMergeRequestListResult))]
[JsonSerializable(typeof(MilestoneSummary))]
[JsonSerializable(typeof(MilestoneListResult))]
[JsonSerializable(typeof(BurndownEventSummary))]
[JsonSerializable(typeof(BurndownEventListResult))]
[JsonSerializable(typeof(BoardSummary))]
[JsonSerializable(typeof(BoardSummaryListResult))]
[JsonSerializable(typeof(BoardColumnSummary))]
[JsonSerializable(typeof(BoardColumnListResult))]
[JsonSerializable(typeof(LabelSummary))]
[JsonSerializable(typeof(LabelListResult))]
[JsonSerializable(typeof(IterationSummary))]
[JsonSerializable(typeof(IterationListResult))]
[JsonSerializable(typeof(TodoSummary))]
[JsonSerializable(typeof(TodoListResult))]
[JsonSerializable(typeof(ResourceGroupSummary))]
[JsonSerializable(typeof(ResourceGroupListResult))]
[JsonSerializable(typeof(ResourceEventSummary))]
[JsonSerializable(typeof(ResourceEventListResult))]
[JsonSerializable(typeof(MilestoneDeleteResult))]
[JsonSerializable(typeof(MilestonePromoteResult))]
[JsonSerializable(typeof(MilestoneMergeRequestListResult))]
[JsonSerializable(typeof(LabelDeleteResult))]
[JsonSerializable(typeof(LabelPromoteResult))]
[JsonSerializable(typeof(BoardDetail))]
[JsonSerializable(typeof(BoardDeleteResult))]
[JsonSerializable(typeof(BoardListDeleteResult))]
[JsonSerializable(typeof(MarkAllTodosDoneResult))]
[JsonSerializable(typeof(MergeRequestSubscriptionResult))]
[JsonSerializable(typeof(JobSummary))]
[JsonSerializable(typeof(ResourceGroupDetail))]
public sealed partial class PlanningJsonContext : JsonSerializerContext;