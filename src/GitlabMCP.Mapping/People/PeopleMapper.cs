using GitLab.Client.Models;
using GitlabMCP.Contracts.People;

namespace GitlabMCP.Mapping.People;

/// <summary>
///     Projects <c>GitLab.Client.Models.*</c> DTOs into the owned People records — never hands back the library's own
///     type.
/// </summary>
public static class PeopleMapper
{
    public static EnterpriseUserSummary ToSummary(GitLabUser user)
    {
        return new EnterpriseUserSummary(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.Locked,
            user.WebUrl?.ToString(),
            user.CreatedAt);
    }

    public static ImpersonationTokenSummary ToSummary(GitLabImpersonationToken token)
    {
        return new ImpersonationTokenSummary(
            token.Id,
            token.Name,
            token.Active,
            token.Revoked,
            token.Scopes ?? [],
            token.CreatedAt,
            token.ExpiresAt,
            token.LastUsedAt);
    }

    public static ImpersonationTokenCreated ToCreated(GitLabImpersonationTokenWithSecret token)
    {
        return new ImpersonationTokenCreated(
            token.Id,
            token.Name,
            token.Token ??
            throw new InvalidOperationException("GitLab did not return the impersonation token's plaintext secret."),
            token.Scopes ?? [],
            token.CreatedAt,
            token.ExpiresAt);
    }

    public static MemberSummary ToSummary(GitLabMember member)
    {
        return new MemberSummary(
            member.Id,
            member.Username,
            member.Name,
            member.State,
            member.AccessLevel,
            member.Locked,
            member.IsUsingSeat,
            member.CreatedAt,
            member.ExpiresAt,
            member.WebUrl?.ToString());
    }

    public static SshKeySummary ToSummary(GitLabSshKey key)
    {
        return new SshKeySummary(
            key.Id,
            key.Title,
            key.Key,
            key.UsageType,
            key.CreatedAt,
            key.ExpiresAt,
            key.LastUsedAt,
            key.User is null ? null : ToSummary(key.User));
    }

    public static ResourceAccessTokenSummary ToSummary(GitLabAccessToken token)
    {
        return new ResourceAccessTokenSummary(
            token.Id,
            token.Name,
            token.Description,
            token.Active,
            token.Revoked,
            token.Scopes ?? [],
            token.AccessLevel,
            token.CreatedAt,
            token.ExpiresAt,
            token.LastUsedAt);
    }

    public static ResourceAccessTokenCreated ToCreated(GitLabAccessTokenWithSecret token)
    {
        return new ResourceAccessTokenCreated(
            token.Id,
            token.Name,
            token.Token ??
            throw new InvalidOperationException("GitLab did not return the access token's plaintext secret."),
            token.Scopes ?? [],
            token.AccessLevel,
            token.CreatedAt,
            token.ExpiresAt);
    }

    public static ServiceAccountSummary ToSummary(GitLabServiceAccount account)
    {
        return new ServiceAccountSummary(
            account.Id,
            account.Username,
            account.Name,
            account.PublicEmail);
    }

    public static MemberRoleSummary ToSummary(GitLabMemberRole role)
    {
        return new MemberRoleSummary(
            role.Id,
            role.GroupId,
            role.Name,
            role.Description,
            role.BaseAccessLevel,
            EnabledAbilities(role));
    }

    public static AccessRequestSummary ToSummary(GitLabAccessRequest request)
    {
        return new AccessRequestSummary(
            request.Id,
            request.Username,
            request.Name,
            request.State,
            request.WebUrl?.ToString(),
            request.RequestedAt);
    }

    public static RunnerRegistrationResult ToResult(GitLabRunnerRegistration registration)
    {
        return new RunnerRegistrationResult(
            registration.Id,
            registration.Token,
            registration.TokenExpiresAt);
    }

    public static EnterpriseUserUpdateResult ToUpdateResult(GitLabUser user)
    {
        return new EnterpriseUserUpdateResult(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.WebUrl?.ToString(),
            user.ProjectsLimit,
            user.CanCreateGroup);
    }

    public static InvitationResult ToResult(GitLabInvitation invitation)
    {
        return new InvitationResult(
            invitation.AccessLevel,
            invitation.InviteEmail,
            invitation.UserName,
            invitation.CreatedByName,
            invitation.CreatedAt,
            invitation.ExpiresAt);
    }

    /// <summary>
    ///     Every field on <c>GitLabPendingMember</c> is optional by design — an unredeemed email invite
    ///     carries only <c>Email</c>. See <see cref="PendingGroupMemberSummary" />'s own doc for why
    ///     <c>Email</c> is projected here.
    /// </summary>
    public static PendingGroupMemberSummary ToSummary(GitLabPendingMember member)
    {
        return new PendingGroupMemberSummary(
            member.Id,
            member.Name,
            member.Username,
            member.Email,
            member.WebUrl?.ToString(),
            member.Approved,
            member.Invited);
    }

    public static SamlGroupLinkSummary ToSummary(GitLabSamlGroupLink link)
    {
        return new SamlGroupLinkSummary(
            link.Name,
            link.AccessLevel,
            link.MemberRoleId,
            link.Provider);
    }

    public static ProviderIdentitySummary ToSummary(GitLabProviderIdentity identity)
    {
        return new ProviderIdentitySummary(
            identity.ExternUid,
            identity.UserId,
            identity.Active);
    }

    // ---- backlog-split/people/part-2.json below ----

    public static NotificationSettingsResult ToResult(GitLabNotificationSettings settings)
    {
        return new NotificationSettingsResult(
            NotificationLevelToString(settings.Level),
            settings.NotificationEmail,
            EnabledNotificationEvents(settings));
    }

    public static UserCountsSummary ToSummary(GitLabUserCounts counts)
    {
        return new UserCountsSummary(
            counts.MergeRequests,
            counts.AssignedIssues,
            counts.AssignedMergeRequests,
            counts.ReviewRequestedMergeRequests,
            counts.Todos);
    }

    public static EmailSummary ToSummary(GitLabEmail email)
    {
        return new EmailSummary(
            email.Id,
            email.Email,
            email.ConfirmedAt);
    }

    public static UserPreferencesResult ToResult(GitLabUserPreferences preferences)
    {
        return new UserPreferencesResult(
            preferences.ViewDiffsFileByFile,
            preferences.ShowWhitespaceInDiffs,
            preferences.PassUserIdentitiesToCiJwt,
            preferences.PolicyAdvancedEditor);
    }

    public static UserStatusResult ToResult(GitLabUserStatus status)
    {
        return new UserStatusResult(
            status.Emoji,
            status.Message,
            status.Availability,
            status.ClearStatusAt);
    }

    public static SupportPinResult ToResult(GitLabSupportPin pin)
    {
        return new SupportPinResult(
            pin.Pin,
            pin.ExpiresAt);
    }

    public static UserManageResult ToManageResult(GitLabUser user)
    {
        return new UserManageResult(
            user.Id,
            user.Username,
            user.Name,
            user.State,
            user.WebUrl?.ToString(),
            user.CreatedAt);
    }

    public static PersonalAccessTokenSummary ToResult(GitLabPersonalAccessToken token)
    {
        return new PersonalAccessTokenSummary(
            token.Id,
            token.Name,
            token.Description,
            token.Active,
            token.Revoked,
            token.Scopes ?? [],
            token.UserId,
            token.CreatedAt,
            token.ExpiresAt,
            token.LastUsedAt);
    }

    public static PersonalAccessTokenCreated ToCreated(GitLabPersonalAccessTokenWithSecret token)
    {
        return new PersonalAccessTokenCreated(
            token.Id,
            token.Name,
            token.Token ??
            throw new InvalidOperationException("GitLab did not return the personal access token's plaintext secret."),
            token.Scopes ?? [],
            token.UserId,
            token.CreatedAt,
            token.ExpiresAt);
    }

    // ---- backlog-split/people/part-3.json below ----

    /// <summary>
    ///     <c>GitLabGroupManagedSshKey</c> (the group-credentials-inventory shape) has no <c>Key</c> member —
    ///     see <see cref="GroupManagedSshKeySummary" />'s own doc.
    /// </summary>
    public static GroupManagedSshKeySummary ToSummary(GitLabGroupManagedSshKey key)
    {
        return new GroupManagedSshKeySummary(
            key.Id,
            key.Title,
            key.UsageType,
            key.UserId,
            key.CreatedAt,
            key.ExpiresAt,
            key.LastUsedAt);
    }

    public static SshCertificateSummary ToSummary(GitLabSshCertificate certificate)
    {
        return new SshCertificateSummary(
            certificate.Id,
            certificate.Title,
            certificate.Key,
            certificate.CreatedAt);
    }

    public static GpgKeySummary ToSummary(GitLabGpgKey key)
    {
        return new GpgKeySummary(
            key.Id,
            key.Key,
            key.CreatedAt);
    }

    private static string? NotificationLevelToString(GitLabNotificationLevel level)
    {
        return level switch
        {
            GitLabNotificationLevel.Disabled => "disabled",
            GitLabNotificationLevel.Participating => "participating",
            GitLabNotificationLevel.Watch => "watch",
            GitLabNotificationLevel.Global => "global",
            GitLabNotificationLevel.Mention => "mention",
            GitLabNotificationLevel.Custom => "custom",
            _ => level.ToString()
        };
    }

    /// <summary>
    ///     <see cref="GitLabNotificationSettings" /> spreads 24 grantable event flags across one bool property
    ///     each (<c>NewIssue</c>, <c>FailedPipeline</c>, <c>Approver</c>, ...). Collapsing to the ones actually
    ///     turned on keeps <see cref="NotificationSettingsResult" /> a handful of fields instead of 24 mostly
    ///     -false ones — the same pattern as <see cref="EnabledAbilities" /> above. Property list verified by
    ///     reflection over GitLab.Client.dll 1.0.0. Keys are kept in exact 1:1 correspondence with
    ///     PeopleTools.NotificationEventSetters' write-side mapping.
    /// </summary>
    internal static IReadOnlyList<string> EnabledNotificationEvents(GitLabNotificationSettings settings)
    {
        List<string> events = [];

        void Add(bool? flag, string name)
        {
            if (flag == true) events.Add(name);
        }

        Add(settings.NewRelease, "new_release");
        Add(settings.NewNote, "new_note");
        Add(settings.NewIssue, "new_issue");
        Add(settings.ReopenIssue, "reopen_issue");
        Add(settings.CloseIssue, "close_issue");
        Add(settings.ReassignIssue, "reassign_issue");
        Add(settings.IssueDue, "issue_due");
        Add(settings.NewMergeRequest, "new_merge_request");
        Add(settings.PushToMergeRequest, "push_to_merge_request");
        Add(settings.ReopenMergeRequest, "reopen_merge_request");
        Add(settings.CloseMergeRequest, "close_merge_request");
        Add(settings.ReassignMergeRequest, "reassign_merge_request");
        Add(settings.ChangeReviewerMergeRequest, "change_reviewer_merge_request");
        Add(settings.MergeMergeRequest, "merge_merge_request");
        Add(settings.FailedPipeline, "failed_pipeline");
        Add(settings.FixedPipeline, "fixed_pipeline");
        Add(settings.SuccessPipeline, "success_pipeline");
        Add(settings.MovedProject, "moved_project");
        Add(settings.MergeWhenPipelineSucceeds, "merge_when_pipeline_succeeds");
        Add(settings.NewEpic, "new_epic");
        Add(settings.ServiceAccountFailedPipeline, "service_account_failed_pipeline");
        Add(settings.ServiceAccountSuccessPipeline, "service_account_success_pipeline");
        Add(settings.ServiceAccountFixedPipeline, "service_account_fixed_pipeline");
        Add(settings.Approver, "approver");

        return events;
    }

    /// <summary>
    ///     <see cref="GitLabMemberRole" /> spreads ~40 grantable abilities across one bool property each
    ///     (<c>AdminRunners</c>, <c>ReadCode</c>, <c>ManageDeployTokens</c>, ...). Collapsing to the ones
    ///     actually turned on keeps <see cref="MemberRoleSummary" /> a handful of fields instead of ~40 mostly
    ///     -false ones (mcp-tool-authoring Step 4's "project, don't dump" rule). Property list verified by
    ///     reflection over GitLab.Client.dll 1.0.0.
    /// </summary>
    internal static IReadOnlyList<string> EnabledAbilities(GitLabMemberRole role)
    {
        List<string> abilities = [];

        void Add(bool? flag, string name)
        {
            if (flag == true) abilities.Add(name);
        }

        Add(role.ApplySecurityScanProfiles, "apply_security_scan_profiles");
        Add(role.AdminMergeRequest, "admin_merge_request");
        Add(role.ArchiveProject, "archive_project");
        Add(role.AdminAiCatalogItemConsumer, "admin_ai_catalog_item_consumer");
        Add(role.CreateSecurityScanProfiles, "create_security_scan_profiles");
        Add(role.DestroyPackage, "destroy_package");
        Add(role.RemoveProject, "remove_project");
        Add(role.DeleteSecurityScanProfiles, "delete_security_scan_profiles");
        Add(role.RemoveGroup, "remove_group");
        Add(role.ManageSecurityPolicyLink, "manage_security_policy_link");
        Add(role.AdminAiCatalogItem, "admin_ai_catalog_item");
        Add(role.AdminComplianceFramework, "admin_compliance_framework");
        Add(role.AdminCicdVariables, "admin_cicd_variables");
        Add(role.ManageDeployTokens, "manage_deploy_tokens");
        Add(role.ManageGroupAccessTokens, "manage_group_access_tokens");
        Add(role.AdminGroupMember, "admin_group_member");
        Add(role.AdminIntegrations, "admin_integrations");
        Add(role.ManageMergeRequestSettings, "manage_merge_request_settings");
        Add(role.ManageProjectAccessTokens, "manage_project_access_tokens");
        Add(role.AdminProtectedBranch, "admin_protected_branch");
        Add(role.AdminProtectedEnvironments, "admin_protected_environments");
        Add(role.AdminPushRules, "admin_push_rules");
        Add(role.AdminRunners, "admin_runners");
        Add(role.AdminSecurityAttributes, "admin_security_attributes");
        Add(role.AdminTerraformState, "admin_terraform_state");
        Add(role.AdminVulnerability, "admin_vulnerability");
        Add(role.AdminWebHook, "admin_web_hook");
        Add(role.ReadAgentArtifacts, "read_agent_artifacts");
        Add(role.ReadComplianceDashboard, "read_compliance_dashboard");
        Add(role.ReadSecurityScanProfiles, "read_security_scan_profiles");
        Add(role.ReadVirtualRegistry, "read_virtual_registry");
        Add(role.UpdateSecAiWorkflowSettings, "update_sec_ai_workflow_settings");
        Add(role.UpdateSecurityScanProfiles, "update_security_scan_profiles");
        Add(role.ReadAdminCicd, "read_admin_cicd");
        Add(role.ReadCrmContact, "read_crm_contact");
        Add(role.ReadDependency, "read_dependency");
        Add(role.ReadAdminGroups, "read_admin_groups");
        Add(role.ReadAdminProjects, "read_admin_projects");
        Add(role.ReadCode, "read_code");
        Add(role.ReadRunners, "read_runners");
        Add(role.ReadSecurityAttribute, "read_security_attribute");
        Add(role.ReadAdminSubscription, "read_admin_subscription");
        Add(role.ReadAdminMonitoring, "read_admin_monitoring");
        Add(role.ReadAdminUsers, "read_admin_users");
        Add(role.ReadVulnerability, "read_vulnerability");

        return abilities;
    }
}