using GitLab.Client.Models;
using GitlabMCP.Contracts.Projects;

namespace GitlabMCP.Mapping.Projects;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> DTOs into the owned records in
///     <see cref="GitlabMCP.Contracts.Projects" /> — never hands back the library type itself.
/// </summary>
public static class ProjectMapper
{
    public static ProjectSummary ToSummary(GitLabProject project)
    {
        return new ProjectSummary(
            project.Id,
            project.Name,
            project.PathWithNamespace,
            project.Description,
            project.Visibility.ToString(),
            project.WebUrl?.ToString(),
            project.DefaultBranch,
            project.CreatedAt,
            project.Archived,
            project.StarCount,
            project.ForksCount);
    }

    public static GroupSummary ToSummary(GitLabGroup group)
    {
        return new GroupSummary(
            group.Id,
            group.Name,
            group.Path,
            group.Description,
            group.Visibility.ToString(),
            group.WebUrl?.ToString(),
            group.FullPath,
            group.ParentId,
            group.CreatedAt,
            group.Archived);
    }

    public static GroupIssueSummary ToGroupIssueSummary(GitLabIssue issue)
    {
        return new GroupIssueSummary(
            issue.Iid,
            issue.ProjectId,
            issue.Title,
            issue.State,
            issue.Author?.Username,
            issue.Labels ?? [],
            issue.WebUrl?.ToString(),
            issue.UpdatedAt);
    }

    public static NamespaceStorageLimitExclusionSummary ToSummary(GitLabNamespaceStorageLimitExclusion exclusion)
    {
        return new NamespaceStorageLimitExclusionSummary(
            exclusion.Id,
            exclusion.NamespaceId,
            exclusion.NamespaceName,
            exclusion.Reason);
    }

    public static ProjectUploadSummary ToSummary(GitLabProjectUpload upload)
    {
        return new ProjectUploadSummary(
            upload.Id,
            upload.Filename,
            upload.Size,
            upload.CreatedAt,
            upload.UploadedBy?.Username);
    }

    public static ProjectAliasSummary ToSummary(GitLabProjectAlias alias)
    {
        return new ProjectAliasSummary(
            alias.Id,
            alias.ProjectId,
            alias.Name);
    }

    public static TopicSummary ToSummary(GitLabTopic topic)
    {
        return new TopicSummary(
            topic.Id,
            topic.Name,
            topic.Title,
            topic.Description,
            topic.TotalProjectsCount,
            topic.AvatarUrl?.ToString());
    }

    public static BadgeSummary ToSummary(GitLabBadge badge)
    {
        return new BadgeSummary(
            badge.Id,
            badge.Name,
            badge.LinkUrl,
            badge.ImageUrl,
            badge.RenderedLinkUrl?.ToString(),
            badge.RenderedImageUrl?.ToString(),
            badge.Kind);
    }

    public static CustomAttributeSummary ToSummary(GitLabCustomAttribute attribute)
    {
        return new CustomAttributeSummary(
            attribute.Key,
            attribute.Value);
    }

    public static AvatarResult ToResult(GitLabAvatar avatar)
    {
        return new AvatarResult(
            avatar.AvatarUrl?.ToString());
    }

    public static StorageMoveSummary ToSummary(GitLabProjectRepositoryStorageMove move)
    {
        return new StorageMoveSummary(
            move.Id,
            move.State.ToString(),
            move.CreatedAt,
            move.SourceStorageName,
            move.DestinationStorageName,
            move.ErrorMessage,
            move.Project?.Id,
            move.Project?.PathWithNamespace);
    }

    public static CiConfigMergeRequestResult ToResult(GitLabMergeRequest mergeRequest)
    {
        return new CiConfigMergeRequestResult(
            mergeRequest.Iid,
            mergeRequest.ProjectId,
            mergeRequest.Title,
            mergeRequest.State,
            mergeRequest.SourceBranch,
            mergeRequest.TargetBranch,
            mergeRequest.WebUrl?.ToString());
    }

    /// <summary>
    ///     Never projects <see cref="GitLabUser.Email" />, <see cref="GitLabUser.CommitEmail" />,
    ///     <see cref="GitLabUser.Identities" />, <see cref="GitLabUser.ScimIdentities" />,
    ///     <see cref="GitLabUser.IsAdmin" />, <see cref="GitLabUser.Note" /> or
    ///     <see cref="GitLabUser.CustomAttributes" /> — none of a member-lookup tool's business.
    /// </summary>
    public static ProjectMemberSummary ToMemberSummary(GitLabUser user)
    {
        return new ProjectMemberSummary(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.WebUrl?.ToString());
    }

    public static RelatedGroupSummary ToRelatedGroupSummary(GitLabPublicGroupDetails group)
    {
        return new RelatedGroupSummary(
            group.Id,
            group.Name,
            group.FullName,
            group.FullPath,
            group.WebUrl?.ToString());
    }

    /// <summary>
    ///     Overload for the two related-group methods that answer with the fuller <see cref="GitLabGroup" /> shape
    ///     instead — never projects <see cref="GitLabGroup.RunnersToken" />.
    /// </summary>
    public static RelatedGroupSummary ToRelatedGroupSummary(GitLabGroup group)
    {
        return new RelatedGroupSummary(
            group.Id,
            group.Name,
            group.FullName,
            group.FullPath,
            group.WebUrl?.ToString());
    }

    /// <summary>
    ///     Never projects <see cref="GitLabBillableMember.Email" /> or <see cref="GitLabBillableMember.PublicEmail" /> —
    ///     PII not needed by a seat-holder listing.
    /// </summary>
    public static GroupSpecialUserSummary ToSummary(GitLabBillableMember member)
    {
        return new GroupSpecialUserSummary(
            member.Id,
            member.Username,
            member.Name,
            member.State,
            member.WebUrl?.ToString(),
            member.MembershipType,
            member.TwoFactorEnabled);
    }

    /// <summary>
    ///     Overload for the provisioned-users/SAML-users methods, which answer with the fuller
    ///     <see cref="GitLabUser" /> shape instead. Never projects <see cref="GitLabUser.Email" />,
    ///     <see cref="GitLabUser.CommitEmail" />, <see cref="GitLabUser.Identities" />,
    ///     <see cref="GitLabUser.ScimIdentities" />, <see cref="GitLabUser.IsAdmin" />,
    ///     <see cref="GitLabUser.Note" /> or <see cref="GitLabUser.CustomAttributes" />.
    /// </summary>
    public static GroupSpecialUserSummary ToGroupSpecialUserSummary(GitLabUser user)
    {
        return new GroupSpecialUserSummary(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.WebUrl?.ToString(),
            null,
            user.TwoFactorEnabled);
    }

    /// <summary>
    ///     <see cref="GitLabGroupAuditEvent.Details" /> is re-serialized to a JSON string rather than returned as a raw
    ///     untyped JsonElement.
    /// </summary>
    public static GroupAuditEventResult ToResult(GitLabGroupAuditEvent auditEvent)
    {
        return new GroupAuditEventResult(
            auditEvent.Id,
            auditEvent.AuthorId,
            auditEvent.EntityId,
            auditEvent.EntityType,
            auditEvent.EventName,
            auditEvent.Details is { } details ? details.GetRawText() : null,
            auditEvent.CreatedAt);
    }

    public static ProjectUploadLinkResult ToSummary(GitLabProjectUploadLink link)
    {
        return new ProjectUploadLinkResult(
            link.Id,
            link.Alt,
            link.Url?.ToString(),
            link.FullPath,
            link.Markdown);
    }

    public static NamespaceSummary ToSummary(GitLabNamespace ns)
    {
        return new NamespaceSummary(
            ns.Id,
            ns.Name,
            ns.Path,
            ns.Kind,
            ns.FullPath,
            ns.ParentId,
            ns.WebUrl?.ToString(),
            ns.ProjectsCount,
            ns.Plan);
    }

    public static NamespaceSubscriptionResult ToResult(GitLabNamespaceSubscription subscription)
    {
        return new NamespaceSubscriptionResult(
            subscription.Plan?.Code,
            subscription.Plan?.Name,
            subscription.Plan?.Trial,
            subscription.Plan?.AutoRenew,
            subscription.Plan?.Upgradable,
            subscription.Usage?.SeatsInSubscription,
            subscription.Usage?.SeatsInUse,
            subscription.Usage?.MaxSeatsUsed,
            subscription.Usage?.SeatsOwed,
            subscription.Billing?.SubscriptionStartDate,
            subscription.Billing?.SubscriptionEndDate,
            subscription.Billing?.TrialEndsOn);
    }

    // -----------------------------------------------------------------------------------------
    // Chunk 3 additions: storage-move creation (one overload per entity kind) and organizations.

    public static StorageMoveCreateResult ToCreateResult(GitLabProjectRepositoryStorageMove move)
    {
        return new StorageMoveCreateResult(
            move.Id,
            move.State.ToString(),
            move.CreatedAt,
            move.SourceStorageName,
            move.DestinationStorageName,
            move.ErrorMessage,
            move.Project?.Id,
            move.Project?.PathWithNamespace);
    }

    /// <summary>Overload for a group (wiki) storage move — a distinct GitLab.Client model from the project one above.</summary>
    public static StorageMoveCreateResult ToCreateResult(GitLabGroupRepositoryStorageMove move)
    {
        return new StorageMoveCreateResult(
            move.Id,
            move.State.ToString(),
            move.CreatedAt,
            move.SourceStorageName,
            move.DestinationStorageName,
            move.ErrorMessage,
            move.Group?.Id,
            move.Group?.Name);
    }

    /// <summary>
    ///     Overload for a snippet storage move. Never projects <see cref="GitLabStorageMoveSnippet.Author" /> —
    ///     that field is a full <c>GitLabUser</c>, out of scope for a storage-move result.
    /// </summary>
    public static StorageMoveCreateResult ToCreateResult(GitLabSnippetRepositoryStorageMove move)
    {
        return new StorageMoveCreateResult(
            move.Id,
            move.State.ToString(),
            move.CreatedAt,
            move.SourceStorageName,
            move.DestinationStorageName,
            move.ErrorMessage,
            move.Snippet?.Id,
            move.Snippet?.Title);
    }

    public static OrganizationSummary ToSummary(GitLabOrganization organization)
    {
        return new OrganizationSummary(
            organization.Id,
            organization.Name,
            organization.Path,
            organization.Description,
            organization.Visibility,
            organization.WebUrl?.ToString(),
            organization.AvatarUrl?.ToString(),
            organization.CreatedAt);
    }
}