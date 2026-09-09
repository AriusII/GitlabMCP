namespace GitlabMCP.Contracts.Projects;

/// <summary>
///     Projects/groups/namespaces projection records. Every record here that carries a GitLab-authored
///     string (a name, path, description, filename, error message, ...) is returned only via
///     <c>GitLabContent.Wrap</c> — never raw. The handful that hold nothing but caller-supplied ids and
///     server-computed booleans (the delete-result records) are the deliberate bare-record exception.
/// </summary>
public sealed record ProjectSummary(
    long Id,
    string? Name,
    string? PathWithNamespace,
    string? Description,
    string? Visibility,
    string? WebUrl,
    string? DefaultBranch,
    DateTimeOffset? CreatedAt,
    bool? Archived,
    int? StarCount,
    int? ForksCount);

public sealed record ProjectListResult(IReadOnlyList<ProjectSummary> Projects, bool Truncated);

public sealed record GroupSummary(
    long Id,
    string? Name,
    string? Path,
    string? Description,
    string? Visibility,
    string? WebUrl,
    string? FullPath,
    long? ParentId,
    DateTimeOffset? CreatedAt,
    bool? Archived);

public sealed record GroupIssueSummary(
    long Iid,
    long? ProjectId,
    string? Title,
    string? State,
    string? Author,
    IReadOnlyList<string> Labels,
    string? WebUrl,
    DateTimeOffset? UpdatedAt);

public sealed record GroupIssueListResult(IReadOnlyList<GroupIssueSummary> Issues, bool Truncated);

public sealed record NamespaceStorageLimitExclusionSummary(
    long Id,
    long? NamespaceId,
    string? NamespaceName,
    string? Reason);

public sealed record NamespaceStorageLimitExclusionListResult(
    IReadOnlyList<NamespaceStorageLimitExclusionSummary> Exclusions,
    bool Truncated);

public sealed record ProjectUploadSummary(
    long Id,
    string? Filename,
    long? Size,
    DateTimeOffset? CreatedAt,
    string? UploadedByUsername);

public sealed record ProjectUploadListResult(IReadOnlyList<ProjectUploadSummary> Uploads, bool Truncated);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record ProjectUploadDeleteResult(bool Deleted);

public sealed record ProjectAliasSummary(long Id, long ProjectId, string? Name);

public sealed record ProjectAliasListResult(IReadOnlyList<ProjectAliasSummary> Aliases, bool Truncated);

public sealed record TopicSummary(
    long Id,
    string? Name,
    string? Title,
    string? Description,
    int? TotalProjectsCount,
    string? AvatarUrl);

public sealed record TopicListResult(IReadOnlyList<TopicSummary> Topics, bool Truncated);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record TopicDeleteResult(bool Deleted);

public sealed record BadgeSummary(
    long Id,
    string? Name,
    string? LinkUrl,
    string? ImageUrl,
    string? RenderedLinkUrl,
    string? RenderedImageUrl,
    string? Kind);

public sealed record BadgeListResult(IReadOnlyList<BadgeSummary> Badges, bool Truncated);

/// <summary>
///     <see cref="Value" /> is GitLab's opaque, caller-defined attribute value (DEC — reviewed exception to
///     the "no bare .Value" grep rule in mcp-untrusted-content Step 3: this tool's entire purpose is
///     reading/writing that value, it is not a credential field, and it is distinct from
///     <c>GitLabVariable.Value</c> / <c>*WithSecret.Token</c>, which remain forbidden).
/// </summary>
public sealed record CustomAttributeSummary(string Key, string? Value);

public sealed record CustomAttributeListResult(IReadOnlyList<CustomAttributeSummary> Attributes, bool Truncated);

public sealed record AvatarResult(string? AvatarUrl);

public sealed record StorageMoveSummary(
    long Id,
    string? State,
    DateTimeOffset? CreatedAt,
    string? SourceStorageName,
    string? DestinationStorageName,
    string? ErrorMessage,
    long? ProjectId,
    string? ProjectPathWithNamespace);

public sealed record StorageMoveListResult(IReadOnlyList<StorageMoveSummary> Moves, bool Truncated);

public sealed record CiConfigMergeRequestResult(
    long Iid,
    long? ProjectId,
    string? Title,
    string? State,
    string? SourceBranch,
    string? TargetBranch,
    string? WebUrl);

public sealed record ProjectMemberSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl);

public sealed record ProjectMemberListResult(IReadOnlyList<ProjectMemberSummary> Members, bool Truncated);

public sealed record ProjectInsightsResult(
    long? FetchesTotal,
    IReadOnlyDictionary<string, double>? Languages,
    string? StorageDiskPath,
    string? StorageRepositoryStorage);

/// <summary>
///     Shared shape for a related-group entry regardless of which of the three GitLab.Client models produced
///     it (<c>GitLabPublicGroupDetails</c> for ancestors, <c>GitLabGroup</c> for invited/share-location
///     results) — the fields below are the intersection both types actually carry.
/// </summary>
public sealed record RelatedGroupSummary(long Id, string? Name, string? FullName, string? FullPath, string? WebUrl);

public sealed record RelatedGroupListResult(IReadOnlyList<RelatedGroupSummary> Groups, bool Truncated);

/// <summary>
///     Bare record: <c>GitLabProjectGroupLink</c> carries only numeric ids, an access-level code and a date —
///     no GitLab-authored string anywhere in it, so this stays outside <c>GitLabContent.Wrap</c>.
/// </summary>
public sealed record ProjectGroupShareResult(
    long? LinkId,
    long? ProjectId,
    long? GroupId,
    int? GroupAccess,
    DateOnly? ExpiresAt,
    bool Unshared);

public sealed record ProjectTransferResult(
    ProjectSummary? TransferredTo,
    IReadOnlyList<RelatedGroupSummary> CandidateNamespaces,
    bool Truncated);

/// <summary>
///     Bare record: <c>GitLabProjectSecuritySettings</c> carries only an id, two timestamps and boolean
///     toggles — no GitLab-authored string, so this stays outside <c>GitLabContent.Wrap</c>.
/// </summary>
public sealed record ProjectSecuritySettingsResult(
    long? ProjectId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool? SecretPushProtectionEnabled,
    bool? PreReceiveSecretDetectionEnabled,
    bool? FastDependencyPathsEnabled,
    bool? ValidityChecksEnabled);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record ProjectDeleteResult(bool Deleted);

public sealed record GroupListResult(IReadOnlyList<GroupSummary> Groups, bool Truncated);

// ---------------------------------------------------------------------------------------------
// Chunk 2 additions: group lifecycle/sharing, group special-user/audit reads, group security
// settings, project uploads (upload/download), and namespaces. Every record below that carries a
// GitLab-authored string is returned only via GitLabContent.Wrap; the handful that hold nothing
// but caller-supplied echoes and server-computed booleans/counts are bare-record exceptions,
// named per DEC-007/mcp-untrusted-content Step 1's flat string test.

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record GroupDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Restored" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record GroupRestoreResult(bool Restored);

public sealed record GroupShareResult(GroupSummary? Group, bool Unshared);

public sealed record GroupTransferResult(
    GroupSummary? TransferredTo,
    IReadOnlyList<RelatedGroupSummary> CandidateParents,
    bool Truncated);

/// <summary>
///     Bare record: every field is a server-computed count (<c>GitLabGroupIssueCounts</c> carries only
///     three <c>int?</c> properties) — no GitLab-authored string anywhere in it.
/// </summary>
public sealed record GroupIssueStatisticsResult(int? All, int? Opened, int? Closed);

/// <summary>
///     Shared shape for a billable-seat holder, a provisioned account, or a SAML-identity user,
///     regardless of which of the two GitLab.Client models (<c>GitLabBillableMember</c> or
///     <c>GitLabUser</c>) produced it. Never projects an email address (PII on both source types) or
///     any of the fields <c>GitLabUser</c> forbids (mcp-untrusted-content Step 3).
/// </summary>
public sealed record GroupSpecialUserSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl,
    string? MembershipType,
    bool? TwoFactorEnabled);

public sealed record GroupSpecialUserListResult(IReadOnlyList<GroupSpecialUserSummary> Users, bool Truncated);

/// <summary>
///     <see cref="DetailsJson" /> is the audit event's free-form <c>Details</c> payload, re-serialized to
///     a JSON string — it is GitLab-authored (it can embed label/branch/user-chosen names) and is
///     wrapped like every other string field here, never returned as a raw untyped JsonElement.
/// </summary>
public sealed record GroupAuditEventResult(
    long Id,
    long? AuthorId,
    long? EntityId,
    string? EntityType,
    string? EventName,
    string? DetailsJson,
    DateTimeOffset? CreatedAt);

/// <summary>
///     Bare record: <see cref="Updated" /> is a server-computed bool and <see cref="SecretPushProtectionEnabled" />
///     echoes the caller's own request value back — no GitLab-authored string involved.
/// </summary>
public sealed record GroupSecuritySettingsUpdateResult(bool Updated, bool SecretPushProtectionEnabled);

public sealed record ProjectUploadLinkResult(long Id, string? Alt, string? Url, string? FullPath, string? Markdown);

public sealed record ProjectUploadDownloadResult(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

public sealed record NamespaceSummary(
    long Id,
    string? Name,
    string? Path,
    string? Kind,
    string? FullPath,
    long? ParentId,
    string? WebUrl,
    int? ProjectsCount,
    string? Plan);

public sealed record NamespaceListResult(IReadOnlyList<NamespaceSummary> Namespaces, bool Truncated);

/// <summary>Flattens <c>GitLabNamespaceSubscription</c>'s three sub-objects (Plan/Usage/Billing) into one record.</summary>
public sealed record NamespaceSubscriptionResult(
    string? PlanCode,
    string? PlanName,
    bool? PlanTrial,
    bool? PlanAutoRenew,
    bool? PlanUpgradable,
    int? UsageSeatsInSubscription,
    int? UsageSeatsInUse,
    int? UsageMaxSeatsUsed,
    int? UsageSeatsOwed,
    DateOnly? BillingSubscriptionStartDate,
    DateOnly? BillingSubscriptionEndDate,
    DateOnly? BillingTrialEndsOn);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record NamespaceStorageLimitExclusionDeleteResult(bool Deleted);

// ---------------------------------------------------------------------------------------------
// Chunk 3 additions: project aliases (get/delete), topics (get/upsert/merge), badges
// (get/update/delete), custom attributes (get/delete), avatar download, storage-move creation,
// and organizations (create/delete). Every record below that carries a GitLab-authored string is
// returned only via GitLabContent.Wrap; the bare-record exceptions are named per DEC-007/
// mcp-untrusted-content Step 1's flat string test.

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record ProjectAliasDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record BadgeDeleteResult(bool Deleted);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record CustomAttributeDeleteResult(bool Deleted);

public sealed record AvatarDownloadResult(
    string? ContentType,
    long? ContentLengthBytes,
    string? FileName,
    string ContentBase64);

/// <summary>
///     Shared shape for the result of scheduling a storage move on a project, a group (wiki), or a
///     snippet, regardless of which of the three distinct GitLab.Client model types
///     (<c>GitLabProjectRepositoryStorageMove</c>, <c>GitLabGroupRepositoryStorageMove</c>,
///     <c>GitLabSnippetRepositoryStorageMove</c>) produced it. <see cref="EntityId" />/
///     <see cref="EntityDescription" /> are the moved entity's own id and name/path/title.
/// </summary>
public sealed record StorageMoveCreateResult(
    long Id,
    string? State,
    DateTimeOffset? CreatedAt,
    string? SourceStorageName,
    string? DestinationStorageName,
    string? ErrorMessage,
    long? EntityId,
    string? EntityDescription);

public sealed record OrganizationSummary(
    long Id,
    string? Name,
    string? Path,
    string? Description,
    string? Visibility,
    string? WebUrl,
    string? AvatarUrl,
    DateTimeOffset? CreatedAt);

/// <summary>Bare record: <see cref="Deleted" /> is a server-computed bool, no GitLab-authored string involved.</summary>
public sealed record OrganizationDeleteResult(bool Deleted);