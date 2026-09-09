using System.Text.Json.Serialization;
using GitlabMCP.Contracts.Projects;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every Projects/groups/namespaces payload record. Kept as its own
///     context/file per CLAUDE.md's Projects-domain instructions: a JsonSerializerContext's
///     [JsonSerializable] attributes must all live in ONE file (splitting across files throws CS8785 on
///     this SDK — see GitlabMcpJsonContext.cs), and this domain's records must not be appended to that
///     shared Epics/Ping context, to keep each domain's build independent of the others.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProjectSummary))]
[JsonSerializable(typeof(ProjectListResult))]
[JsonSerializable(typeof(GroupSummary))]
[JsonSerializable(typeof(GroupIssueSummary))]
[JsonSerializable(typeof(GroupIssueListResult))]
[JsonSerializable(typeof(NamespaceStorageLimitExclusionSummary))]
[JsonSerializable(typeof(NamespaceStorageLimitExclusionListResult))]
[JsonSerializable(typeof(ProjectUploadSummary))]
[JsonSerializable(typeof(ProjectUploadListResult))]
[JsonSerializable(typeof(ProjectUploadDeleteResult))]
[JsonSerializable(typeof(ProjectAliasSummary))]
[JsonSerializable(typeof(ProjectAliasListResult))]
[JsonSerializable(typeof(TopicSummary))]
[JsonSerializable(typeof(TopicListResult))]
[JsonSerializable(typeof(TopicDeleteResult))]
[JsonSerializable(typeof(BadgeSummary))]
[JsonSerializable(typeof(BadgeListResult))]
[JsonSerializable(typeof(CustomAttributeSummary))]
[JsonSerializable(typeof(CustomAttributeListResult))]
[JsonSerializable(typeof(AvatarResult))]
[JsonSerializable(typeof(StorageMoveSummary))]
[JsonSerializable(typeof(StorageMoveListResult))]
[JsonSerializable(typeof(CiConfigMergeRequestResult))]
[JsonSerializable(typeof(ProjectMemberSummary))]
[JsonSerializable(typeof(ProjectMemberListResult))]
[JsonSerializable(typeof(ProjectInsightsResult))]
[JsonSerializable(typeof(RelatedGroupSummary))]
[JsonSerializable(typeof(RelatedGroupListResult))]
[JsonSerializable(typeof(ProjectGroupShareResult))]
[JsonSerializable(typeof(ProjectTransferResult))]
[JsonSerializable(typeof(ProjectSecuritySettingsResult))]
[JsonSerializable(typeof(ProjectDeleteResult))]
[JsonSerializable(typeof(GroupListResult))]
[JsonSerializable(typeof(GroupDeleteResult))]
[JsonSerializable(typeof(GroupRestoreResult))]
[JsonSerializable(typeof(GroupShareResult))]
[JsonSerializable(typeof(GroupTransferResult))]
[JsonSerializable(typeof(GroupIssueStatisticsResult))]
[JsonSerializable(typeof(GroupSpecialUserSummary))]
[JsonSerializable(typeof(GroupSpecialUserListResult))]
[JsonSerializable(typeof(GroupAuditEventResult))]
[JsonSerializable(typeof(GroupSecuritySettingsUpdateResult))]
[JsonSerializable(typeof(ProjectUploadLinkResult))]
[JsonSerializable(typeof(ProjectUploadDownloadResult))]
[JsonSerializable(typeof(NamespaceSummary))]
[JsonSerializable(typeof(NamespaceListResult))]
[JsonSerializable(typeof(NamespaceSubscriptionResult))]
[JsonSerializable(typeof(NamespaceStorageLimitExclusionDeleteResult))]
// Chunk 3 additions (backlog part-3.json).
[JsonSerializable(typeof(ProjectAliasDeleteResult))]
[JsonSerializable(typeof(BadgeDeleteResult))]
[JsonSerializable(typeof(CustomAttributeDeleteResult))]
[JsonSerializable(typeof(AvatarDownloadResult))]
[JsonSerializable(typeof(StorageMoveCreateResult))]
[JsonSerializable(typeof(OrganizationSummary))]
[JsonSerializable(typeof(OrganizationDeleteResult))]
public sealed partial class ProjectsJsonContext : JsonSerializerContext;