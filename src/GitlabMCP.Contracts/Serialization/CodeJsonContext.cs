using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Code;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for the "code" domain (repository/files/branches/commits). Its own
///     context, separate from <see cref="GitlabMcpJsonContext" /> — splitting [JsonSerializable] attributes
///     for the same partial class across two files throws CS8785 on this SDK (see
///     <c>GitlabMcpJsonContext.cs</c>'s own docstring); every domain that lands concurrently gets its own
///     context class instead. The host inserts <see cref="CodeJsonContext.Default" /> into
///     <c>GitLabJson.Options.TypeInfoResolverChain</c> alongside the other per-domain contexts.
///     Every payload/parameter type this domain's tools touch is listed explicitly, including
///     <see cref="CommitFileAction" />'s own <c>IReadOnlyList&lt;&gt;</c> wrapper: a bare tool parameter type
///     is a *root* lookup against the resolver chain, not something reached by walking another registered
///     type's properties, so it needs its own entry even though its element type is also listed.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(CommitCommentSummary))]
[JsonSerializable(typeof(CommitCommentListResult))]
[JsonSerializable(typeof(ContributorSummary))]
[JsonSerializable(typeof(ContributorListResult))]
[JsonSerializable(typeof(CommitStatusSummary))]
[JsonSerializable(typeof(CommitStatusListResult))]
[JsonSerializable(typeof(CommitSummary))]
[JsonSerializable(typeof(CommitListResult))]
[JsonSerializable(typeof(CommitDetailSummary))]
[JsonSerializable(typeof(DiffSummary))]
[JsonSerializable(typeof(DiffListResult))]
[JsonSerializable(typeof(CommitMergeRequestSummary))]
[JsonSerializable(typeof(CommitMergeRequestListResult))]
[JsonSerializable(typeof(CompareResult))]
[JsonSerializable(typeof(BranchSummary))]
[JsonSerializable(typeof(BranchListResult))]
[JsonSerializable(typeof(BranchDeleteResult))]
[JsonSerializable(typeof(MergedBranchesDeleteResult))]
[JsonSerializable(typeof(TagSummary))]
[JsonSerializable(typeof(TagListResult))]
[JsonSerializable(typeof(TagDeleteResult))]
[JsonSerializable(typeof(BlameRangeSummary))]
[JsonSerializable(typeof(BlameResult))]
[JsonSerializable(typeof(RepositoryFileSummary))]
[JsonSerializable(typeof(TreeItemSummary))]
[JsonSerializable(typeof(TreeListResult))]
[JsonSerializable(typeof(FileContentResult))]
[JsonSerializable(typeof(FileDeleteResult))]
[JsonSerializable(typeof(AccessLevelSummary))]
[JsonSerializable(typeof(ProtectedBranchSummary))]
[JsonSerializable(typeof(ProtectedBranchListResult))]
[JsonSerializable(typeof(BranchUnprotectResult))]
[JsonSerializable(typeof(ProtectedTagSummary))]
[JsonSerializable(typeof(ProtectedTagListResult))]
[JsonSerializable(typeof(PushRuleSummary))]
[JsonSerializable(typeof(PullMirrorSummary))]
[JsonSerializable(typeof(CommitFileAction))]
[JsonSerializable(typeof(IReadOnlyList<CommitFileAction>))]
[JsonSerializable(typeof(TagUnprotectResult))]
[JsonSerializable(typeof(RemoteMirrorHostKeySummary))]
[JsonSerializable(typeof(RemoteMirrorSummary))]
[JsonSerializable(typeof(RemoteMirrorListResult))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public sealed partial class CodeJsonContext : JsonSerializerContext;