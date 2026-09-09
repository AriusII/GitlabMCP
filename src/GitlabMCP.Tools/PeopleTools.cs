using System.ComponentModel;
using System.Globalization;
using GitLab.Client.Abstractions;
using GitLab.Client.Models;
using GitlabMCP.Contracts;
using GitlabMCP.Contracts.People;
using GitlabMCP.Mapping.People;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace GitlabMCP.Tools;

/// <summary>
///     People-domain tools: users, members, tokens and access (CLAUDE.md's "Users, members, tokens, access"
///     key). Every tool string here that originates from GitLab (usernames, display names, token names,
///     invitation emails, ...) is GitLab-authored, so every tool wraps via <see cref="GitLabContent" />
///     (mcp-untrusted-content Step 1) EXCEPT the handful whose GitLab call returns no body and whose result
///     is built entirely from the caller's own input — those declare a bare projection record instead
///     (CLAUDE.md rule 4): <see cref="UpdateGroupMemberStateAsync" />, <see cref="RemoveMemberAsync" />,
///     <see cref="ApprovePendingGroupMembersAsync" />, <see cref="DeleteMemberRoleAsync" />,
///     <see cref="DeleteInvitationAsync" />, <see cref="DeleteSamlGroupLinkAsync" />,
///     <see cref="ManageCurrentUserPreferencesAsync" /> (every field is a bool preference flag),
///     <see cref="DeleteUserAsync" />, <see cref="SetUserAccountStateAsync" />,
///     <see cref="RevokePersonalAccessTokenAsync" />, <see cref="RevokeImpersonationTokenAsync" />,
///     <see cref="RevokeResourceAccessTokenAsync" />, <see cref="DeleteSshKeyAsync" />,
///     <see cref="ManageGpgKeyAsync" /> and <see cref="DeleteServiceAccountAsync" />.
///     Plaintext-credential exceptions, per the closed list in CLAUDE.md rule 5 — each mints or reads a
///     credential whose entire purpose is reaching the caller once: <see cref="CreateImpersonationTokenAsync" />,
///     <see cref="CreateCurrentUserRunnerAsync" />, <see cref="CreateResourceAccessTokenAsync" />,
///     <see cref="CreatePersonalAccessTokenAsync" /> (the plaintext token),
///     <see cref="ManageCurrentUserSupportPinAsync" /> (the Support PIN GitLab's own doc says to treat as a
///     secret — it is not an API credential, but the tool's only purpose is handing it to its owner),
///     <see cref="RotatePersonalAccessTokenAsync" />, <see cref="RotateResourceAccessTokenAsync" />,
///     <see cref="ManageServiceAccountAccessTokenAsync" /> (its create/rotate branches) and
///     <see cref="ManageGroupCredentialAsync" /> (its rotate-personal-access-token branch only — GitLab does
///     not return a secret for the resource-access-token rotate branch).
/// </summary>
[McpServerToolType]
public sealed class PeopleTools(
    IUsersClient users,
    IPersonalAccessTokensClient personalAccessTokens,
    IMembersClient members,
    ISshKeysClient sshKeys,
    IAccessTokensClient accessTokens,
    IServiceAccountsClient serviceAccounts,
    IMemberRolesClient memberRoles,
    IAccessRequestsClient accessRequests,
    ICurrentUserClient currentUser,
    IInvitationsClient invitations,
    ISamlGroupLinksClient samlGroupLinks,
    IProviderIdentitiesClient providerIdentities,
    INotificationSettingsClient notificationSettings,
    IGroupCredentialsInventoryClient groupCredentialsInventory,
    IGpgKeysClient gpgKeys)
{
    private const int MaxLimit = 100;

    private const string NotificationEventVocabulary =
        "new_release, new_note, new_issue, reopen_issue, close_issue, reassign_issue, issue_due, " +
        "new_merge_request, push_to_merge_request, reopen_merge_request, close_merge_request, " +
        "reassign_merge_request, change_reviewer_merge_request, merge_merge_request, failed_pipeline, " +
        "fixed_pipeline, success_pipeline, moved_project, merge_when_pipeline_succeeds, new_epic, " +
        "service_account_failed_pipeline, service_account_success_pipeline, service_account_fixed_pipeline, approver";

    // Ability-name vocabulary for gitlab_create_member_role, kept in exact 1:1 correspondence (same
    // string keys) with PeopleMapper.EnabledAbilities' read-side mapping, so a name round-trips: an
    // ability this dictionary can set is exactly one gitlab_list_member_roles can report back.
    private static readonly Dictionary<string, Func<CreateMemberRoleRequest, CreateMemberRoleRequest>> AbilitySetters =
        new(StringComparer.Ordinal)
        {
            ["apply_security_scan_profiles"] = r => r with { ApplySecurityScanProfiles = true },
            ["admin_merge_request"] = r => r with { AdminMergeRequest = true },
            ["archive_project"] = r => r with { ArchiveProject = true },
            ["admin_ai_catalog_item_consumer"] = r => r with { AdminAiCatalogItemConsumer = true },
            ["create_security_scan_profiles"] = r => r with { CreateSecurityScanProfiles = true },
            ["destroy_package"] = r => r with { DestroyPackage = true },
            ["remove_project"] = r => r with { RemoveProject = true },
            ["delete_security_scan_profiles"] = r => r with { DeleteSecurityScanProfiles = true },
            ["remove_group"] = r => r with { RemoveGroup = true },
            ["manage_security_policy_link"] = r => r with { ManageSecurityPolicyLink = true },
            ["admin_ai_catalog_item"] = r => r with { AdminAiCatalogItem = true },
            ["admin_compliance_framework"] = r => r with { AdminComplianceFramework = true },
            ["admin_cicd_variables"] = r => r with { AdminCicdVariables = true },
            ["manage_deploy_tokens"] = r => r with { ManageDeployTokens = true },
            ["manage_group_access_tokens"] = r => r with { ManageGroupAccessTokens = true },
            ["admin_group_member"] = r => r with { AdminGroupMember = true },
            ["admin_integrations"] = r => r with { AdminIntegrations = true },
            ["manage_merge_request_settings"] = r => r with { ManageMergeRequestSettings = true },
            ["manage_project_access_tokens"] = r => r with { ManageProjectAccessTokens = true },
            ["admin_protected_branch"] = r => r with { AdminProtectedBranch = true },
            ["admin_protected_environments"] = r => r with { AdminProtectedEnvironments = true },
            ["admin_push_rules"] = r => r with { AdminPushRules = true },
            ["admin_runners"] = r => r with { AdminRunners = true },
            ["admin_security_attributes"] = r => r with { AdminSecurityAttributes = true },
            ["admin_terraform_state"] = r => r with { AdminTerraformState = true },
            ["admin_vulnerability"] = r => r with { AdminVulnerability = true },
            ["admin_web_hook"] = r => r with { AdminWebHook = true },
            ["read_agent_artifacts"] = r => r with { ReadAgentArtifacts = true },
            ["read_compliance_dashboard"] = r => r with { ReadComplianceDashboard = true },
            ["read_security_scan_profiles"] = r => r with { ReadSecurityScanProfiles = true },
            ["read_virtual_registry"] = r => r with { ReadVirtualRegistry = true },
            ["update_sec_ai_workflow_settings"] = r => r with { UpdateSecAiWorkflowSettings = true },
            ["update_security_scan_profiles"] = r => r with { UpdateSecurityScanProfiles = true },
            ["read_admin_cicd"] = r => r with { ReadAdminCicd = true },
            ["read_crm_contact"] = r => r with { ReadCrmContact = true },
            ["read_dependency"] = r => r with { ReadDependency = true },
            ["read_admin_groups"] = r => r with { ReadAdminGroups = true },
            ["read_admin_projects"] = r => r with { ReadAdminProjects = true },
            ["read_code"] = r => r with { ReadCode = true },
            ["read_runners"] = r => r with { ReadRunners = true },
            ["read_security_attribute"] = r => r with { ReadSecurityAttribute = true },
            ["read_admin_subscription"] = r => r with { ReadAdminSubscription = true },
            ["read_admin_monitoring"] = r => r with { ReadAdminMonitoring = true },
            ["read_admin_users"] = r => r with { ReadAdminUsers = true },
            ["read_vulnerability"] = r => r with { ReadVulnerability = true }
        };

    // The "admin" scope of gitlab_create_member_role uses a different request type with only six of the
    // 45 abilities above (all read_admin_* — an admin role has no base access level to layer onto).
    private static readonly Dictionary<string, Func<CreateAdminMemberRoleRequest, CreateAdminMemberRoleRequest>>
        AdminAbilitySetters = new(StringComparer.Ordinal)
        {
            ["read_admin_cicd"] = r => r with { ReadAdminCicd = true },
            ["read_admin_groups"] = r => r with { ReadAdminGroups = true },
            ["read_admin_projects"] = r => r with { ReadAdminProjects = true },
            ["read_admin_subscription"] = r => r with { ReadAdminSubscription = true },
            ["read_admin_monitoring"] = r => r with { ReadAdminMonitoring = true },
            ["read_admin_users"] = r => r with { ReadAdminUsers = true }
        };

    // Notification-event vocabulary for gitlab_update_notification_settings, kept in exact 1:1
    // correspondence (same string keys) with PeopleMapper.EnabledNotificationEvents' read-side mapping —
    // the same AbilitySetters/EnabledAbilities pattern used above for member roles. Property list verified
    // by reflection over GitLab.Client.dll 1.0.0.
    private static readonly
        Dictionary<string, Func<UpdateNotificationSettingsRequest, bool, UpdateNotificationSettingsRequest>>
        NotificationEventSetters = new(StringComparer.Ordinal)
        {
            ["new_release"] = (r, v) => r with { NewRelease = v },
            ["new_note"] = (r, v) => r with { NewNote = v },
            ["new_issue"] = (r, v) => r with { NewIssue = v },
            ["reopen_issue"] = (r, v) => r with { ReopenIssue = v },
            ["close_issue"] = (r, v) => r with { CloseIssue = v },
            ["reassign_issue"] = (r, v) => r with { ReassignIssue = v },
            ["issue_due"] = (r, v) => r with { IssueDue = v },
            ["new_merge_request"] = (r, v) => r with { NewMergeRequest = v },
            ["push_to_merge_request"] = (r, v) => r with { PushToMergeRequest = v },
            ["reopen_merge_request"] = (r, v) => r with { ReopenMergeRequest = v },
            ["close_merge_request"] = (r, v) => r with { CloseMergeRequest = v },
            ["reassign_merge_request"] = (r, v) => r with { ReassignMergeRequest = v },
            ["change_reviewer_merge_request"] = (r, v) => r with { ChangeReviewerMergeRequest = v },
            ["merge_merge_request"] = (r, v) => r with { MergeMergeRequest = v },
            ["failed_pipeline"] = (r, v) => r with { FailedPipeline = v },
            ["fixed_pipeline"] = (r, v) => r with { FixedPipeline = v },
            ["success_pipeline"] = (r, v) => r with { SuccessPipeline = v },
            ["moved_project"] = (r, v) => r with { MovedProject = v },
            ["merge_when_pipeline_succeeds"] = (r, v) => r with { MergeWhenPipelineSucceeds = v },
            ["new_epic"] = (r, v) => r with { NewEpic = v },
            ["service_account_failed_pipeline"] = (r, v) => r with { ServiceAccountFailedPipeline = v },
            ["service_account_success_pipeline"] = (r, v) => r with { ServiceAccountSuccessPipeline = v },
            ["service_account_fixed_pipeline"] = (r, v) => r with { ServiceAccountFixedPipeline = v },
            ["approver"] = (r, v) => r with { Approver = v }
        };

    /// <summary>
    ///     Every one of <see cref="GetMemberAsync" />, <see cref="AddMemberAsync" />,
    ///     <see cref="UpdateMemberAsync" />, <see cref="RemoveMemberAsync" />, <see cref="RequestAccessAsync" />,
    ///     <see cref="ReviewAccessRequestAsync" />, <see cref="ListInvitationsAsync" />,
    ///     <see cref="UpdateInvitationAsync" />, <see cref="DeleteInvitationAsync" />,
    ///     <see cref="GetResourceAccessTokenAsync" />, <see cref="RotateResourceAccessTokenAsync" />,
    ///     <see cref="RevokeResourceAccessTokenAsync" />, <see cref="ListServiceAccountAccessTokensAsync" /> and
    ///     <see cref="ManageServiceAccountAccessTokenAsync" />, <see cref="GetServiceAccountAsync" /> and
    ///     <see cref="DeleteServiceAccountAsync" /> take a project **or** group route id, never
    ///     both — this is the one place that validation lives.
    /// </summary>
    private static string RequireOneScope(string? project, string? group)
    {
        if (string.IsNullOrWhiteSpace(project) == string.IsNullOrWhiteSpace(group))
            throw new McpException("Provide exactly one of project or group.");

        return string.IsNullOrWhiteSpace(project) ? "group" : "project";
    }

    /// <summary>
    ///     <see cref="ManageServiceAccountAsync" />'s three-way scope validator — unlike
    ///     <see cref="RequireOneScope" />, "instance" needs neither <paramref name="project" /> nor
    ///     <paramref name="group" />, because <c>IServiceAccountsClient.CreateAsync</c>/<c>UpdateAsync</c> are
    ///     instance-scoped with no route id at all.
    /// </summary>
    private static string RequireServiceAccountScope(string scope, string? project, string? group)
    {
        var normalized = scope.ToLowerInvariant();

        switch (normalized)
        {
            case "instance":
                return normalized;

            case "project":
                if (string.IsNullOrWhiteSpace(project))
                    throw new McpException("project is required when scope is \"project\".");

                return normalized;

            case "group":
                if (string.IsNullOrWhiteSpace(group))
                    throw new McpException("group is required when scope is \"group\".");

                return normalized;

            default:
                throw new McpException("scope must be \"instance\", \"group\", or \"project\".");
        }
    }

    private static CreateMemberRoleRequest ApplyAbility(CreateMemberRoleRequest request, string ability)
    {
        if (!AbilitySetters.TryGetValue(ability, out var setter))
            throw new McpException($"Unknown ability \"{ability}\".");

        return setter(request);
    }

    private static CreateAdminMemberRoleRequest ApplyAdminAbility(CreateAdminMemberRoleRequest request, string ability)
    {
        if (!AdminAbilitySetters.TryGetValue(ability, out var setter))
            throw new McpException(
                $"Unknown admin-role ability \"{ability}\". Admin roles only support the six \"read_admin_*\" abilities.");

        return setter(request);
    }

    [McpServerTool(Name = "gitlab_list_enterprise_users", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the SAML/SCIM-provisioned enterprise users claimed by a group. Distinct from ordinary group membership. Administrators and group owners only.")]
    public async Task<CallToolResult> ListEnterpriseUsersAsync(
        [Description("The claiming group's numeric id or full path, e.g. \"my-group\" or \"my-group/subgroup\".")]
        string group,
        [Description("Optional free-text search over name/username/email.")]
        string? search = null,
        [Description("Optional filter: true for active accounts only, false for inactive only, omit for both.")]
        bool? active = null,
        [Description("Maximum users to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new EnterpriseUserListOptions
            { Search = search, Active = active, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<EnterpriseUserSummary> collected = [];
        var truncated = false;

        await foreach (var user in users.ListEnterpriseUsersAsync(group, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(user));
        }

        return GitLabContent.Wrap(new EnterpriseUserListResult(collected, truncated), "groups/:id/enterprise_users");
    }

    [McpServerTool(Name = "gitlab_list_impersonation_tokens", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a user's impersonation tokens (never their plaintext secret). Administrators only; the tokens are invisible to the user they impersonate.")]
    public async Task<CallToolResult> ListImpersonationTokensAsync(
        [Description("The numeric id of the user whose impersonation tokens to list.")]
        long userId,
        [Description("Optional state filter: \"active\" or \"inactive\". Omit for all.")]
        string? state = null,
        [Description("Maximum tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ImpersonationTokenListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<ImpersonationTokenSummary> collected = [];
        var truncated = false;

        await foreach (var token in personalAccessTokens.ListImpersonationTokensAsync(userId, options,
                           cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(token));
        }

        return GitLabContent.Wrap(new ImpersonationTokenListResult(collected, truncated),
            "users/:id/impersonation_tokens");
    }

    [McpServerTool(Name = "gitlab_list_members", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's direct members (not inherited from an ancestor group, not invited-group members). Each entry carries its own access level.")]
    public async Task<CallToolResult> ListMembersAsync(
        [Description("Project: either the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string project,
        [Description("Maximum members to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<MemberSummary> collected = [];
        var truncated = false;

        await foreach (var member in members.ListAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(member));
        }

        return GitLabContent.Wrap(new MemberListResult(collected, truncated), "projects/:id/members");
    }

    [McpServerTool(Name = "gitlab_list_ssh_keys", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the authenticated user's own SSH keys, including each key's public material and usage type.")]
    public async Task<CallToolResult> ListSshKeysAsync(
        [Description("Maximum keys to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<SshKeySummary> collected = [];
        var truncated = false;

        await foreach (var key in sshKeys.ListForCurrentUserAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(key));
        }

        return GitLabContent.Wrap(new SshKeyListResult(collected, truncated), "user/keys");
    }

    [McpServerTool(Name = "gitlab_list_resource_access_tokens", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a project's access tokens (bot-account tokens scoped to that project). Never carries a plaintext secret.")]
    public async Task<CallToolResult> ListResourceAccessTokensAsync(
        [Description("Project: either the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string project,
        [Description("Optional state filter: \"active\" or \"inactive\". Omit for all.")]
        string? state = null,
        [Description("Maximum tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new AccessTokenListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<ResourceAccessTokenSummary> collected = [];
        var truncated = false;

        await foreach (var token in accessTokens.ListForProjectAsync(project, options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(token));
        }

        return GitLabContent.Wrap(new ResourceAccessTokenListResult(collected, truncated),
            "projects/:id/access_tokens");
    }

    [McpServerTool(Name = "gitlab_list_service_accounts", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists service accounts at instance scope. Requires instance administrator rights; a Free-tier instance answers 403 (Premium/Ultimate feature).")]
    public async Task<CallToolResult> ListServiceAccountsAsync(
        [Description("Maximum accounts to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new ServiceAccountListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };

        List<ServiceAccountSummary> collected = [];
        var truncated = false;

        await foreach (var account in serviceAccounts.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(account));
        }

        return GitLabContent.Wrap(new ServiceAccountListResult(collected, truncated), "service_accounts");
    }

    [McpServerTool(Name = "gitlab_list_member_roles", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every instance-level custom member role, with the abilities each one grants. Self-managed and GitLab Dedicated only; requires instance administrator rights; Ultimate-tier feature.")]
    public async Task<CallToolResult> ListMemberRolesAsync(
        [Description("Maximum roles to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<MemberRoleSummary> collected = [];
        var truncated = false;

        await foreach (var role in memberRoles.ListAsync(cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(role));
        }

        return GitLabContent.Wrap(new MemberRoleListResult(collected, truncated), "member_roles");
    }

    [McpServerTool(Name = "gitlab_list_access_requests", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists pending access requests on a project -- people who asked to join and are awaiting approval or denial.")]
    public async Task<CallToolResult> ListAccessRequestsAsync(
        [Description("Project: either the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string project,
        [Description("Maximum requests to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<AccessRequestSummary> collected = [];
        var truncated = false;

        await foreach (var request in accessRequests.ListForProjectAsync(project, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(request));
        }

        return GitLabContent.Wrap(new AccessRequestListResult(collected, truncated), "projects/:id/access_requests");
    }

    [McpServerTool(Name = "gitlab_update_group_member_state", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Moves a user's group membership between the \"awaiting\" and \"active\" states, without removing the member -- how a paid seat is freed or reclaimed.")]
    public async Task<GroupMemberStateUpdateResult> UpdateGroupMemberStateAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The numeric id of the user whose membership state to change.")]
        long userId,
        [Description(
            "The new state: \"awaiting\" (not yet approved, cannot use the group) or \"active\" (approved and in effect).")]
        string state,
        CancellationToken cancellationToken = default)
    {
        var normalized = state.ToLowerInvariant();
        var membershipState = normalized switch
        {
            "awaiting" => GitLabMembershipState.Awaiting,
            "active" => GitLabMembershipState.Active,
            _ => throw new McpException("state must be \"awaiting\" or \"active\".")
        };

        await members.UpdateStateForGroupAsync(group, userId, membershipState, cancellationToken);

        return new GroupMemberStateUpdateResult(userId, normalized);
    }

    [McpServerTool(Name = "gitlab_create_impersonation_token", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a token that acts as another user for both API calls and Git -- the most privileged credential this server can mint. Administrators only. The plaintext secret is returned only in this response; GitLab never discloses it again.")]
    public async Task<CallToolResult> CreateImpersonationTokenAsync(
        [Description("The numeric id of the user the token will impersonate.")]
        long userId,
        [Description("A display name for the token.")]
        string name,
        [Description(
            "At least one GitLab API scope, e.g. \"api\", \"read_api\", \"read_user\", \"read_repository\", \"write_repository\", \"sudo\".")]
        string[] scopes,
        [Description("Optional free-text note describing the token's purpose.")]
        string? description = null,
        [Description(
            "Optional expiry date, ISO 8601 (YYYY-MM-DD). GitLab defaults to 365 days from today when omitted.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Length == 0) throw new McpException("scopes must contain at least one scope.");

        var parsedExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt));

        var request = new CreateImpersonationTokenRequest
        {
            Name = name,
            Scopes = scopes,
            Description = description,
            ExpiresAt = parsedExpiresAt
        };

        var token = await personalAccessTokens.CreateImpersonationTokenAsync(userId, request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToCreated(token), "users/:id/impersonation_tokens (create)");
    }

    [McpServerTool(Name = "gitlab_create_current_user_runner", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Registers a new CI/CD runner owned by the authenticated user. The returned registration token is shown exactly once and cannot be read back from any later call -- persist it immediately.")]
    public async Task<CallToolResult> CreateCurrentUserRunnerAsync(
        [Description("The runner's scope: \"instance_type\", \"group_type\", or \"project_type\".")]
        string runnerType,
        [Description("Required when runnerType is \"group_type\": the numeric id of the owning group.")]
        long? groupId = null,
        [Description("Required when runnerType is \"project_type\": the numeric id of the owning project.")]
        long? projectId = null,
        [Description("Optional human-readable description of the runner.")]
        string? description = null,
        [Description("Optional tags the runner advertises, used to match jobs.")]
        string[]? tagList = null,
        [Description("Whether the runner is locked to its owning project/group. Default false.")]
        bool locked = false,
        [Description("Whether the runner picks up jobs with no matching tags. Default false.")]
        bool runUntagged = false,
        [Description(
            "Optional protected-ref access level: \"not_protected\" or \"ref_protected\". Omit to leave GitLab's default.")]
        string? accessLevel = null,
        [Description("Optional per-job timeout in seconds. Omit to leave GitLab's default.")]
        int? maximumTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (runnerType is not ("instance_type" or "group_type" or "project_type"))
            throw new McpException("runnerType must be \"instance_type\", \"group_type\", or \"project_type\".");

        if (runnerType == "group_type" && groupId is null)
            throw new McpException("groupId is required when runnerType is \"group_type\".");

        if (runnerType == "project_type" && projectId is null)
            throw new McpException("projectId is required when runnerType is \"project_type\".");

        var request = new CreateUserRunnerRequest
        {
            RunnerType = runnerType,
            GroupId = groupId,
            ProjectId = projectId,
            Description = description,
            TagList = tagList,
            Locked = locked,
            RunUntagged = runUntagged,
            AccessLevel = accessLevel,
            MaximumTimeout = maximumTimeout
        };

        var registration = await currentUser.CreateRunnerAsync(request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToResult(registration), "user/runners (create)");
    }

    [McpServerTool(Name = "gitlab_update_enterprise_user", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Updates an enterprise user's name, email, project limit, or ability to create groups. Administrators and group owners only. Fields left unset are unchanged.")]
    public async Task<CallToolResult> UpdateEnterpriseUserAsync(
        [Description("The claiming group's numeric id or full path.")]
        string group,
        [Description("The numeric id of the enterprise user to update.")]
        long userId,
        [Description("New display name, or omit to leave unchanged.")]
        string? name = null,
        [Description("New email address, or omit to leave unchanged.")]
        string? email = null,
        [Description("New personal-project limit, or omit to leave unchanged.")]
        int? projectsLimit = null,
        [Description("Whether the user may create top-level groups, or omit to leave unchanged.")]
        bool? canCreateGroup = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateEnterpriseUserRequest
        {
            Name = name,
            Email = email,
            ProjectsLimit = projectsLimit,
            CanCreateGroup = canCreateGroup
        };

        var user = await users.UpdateEnterpriseUserAsync(group, userId, request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToUpdateResult(user), "groups/:id/enterprise_users/:user_id (update)");
    }

    [McpServerTool(Name = "gitlab_create_invitation", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description("Invites one or more people, by email or numeric user id, to a project at a given access level.")]
    public async Task<CallToolResult> CreateInvitationAsync(
        [Description("Project: either the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string project,
        [Description("The access level to grant: 10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner.")]
        int accessLevel,
        [Description("Email addresses to invite. Provide this, userIds, or both -- at least one is required.")]
        string[]? emails = null,
        [Description(
            "Numeric ids (as strings) of existing users to invite. Provide this, emails, or both -- at least one is required.")]
        string[]? userIds = null,
        [Description("Optional expiry for the invitation/membership, ISO 8601 date-time. Omit for no expiry.")]
        string? expiresAt = null,
        [Description("Optional numeric id of a custom member role to grant instead of a static access level.")]
        long? memberRoleId = null,
        CancellationToken cancellationToken = default)
    {
        if ((emails is null || emails.Length == 0) && (userIds is null || userIds.Length == 0))
            throw new McpException("At least one of emails or userIds must be provided.");

        DateTimeOffset? parsedExpiresAt = null;
        if (expiresAt is not null)
        {
            if (!DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                    out var parsed)) throw new McpException("expiresAt must be a valid ISO 8601 date-time.");

            parsedExpiresAt = parsed;
        }

        var request = new CreateInvitationRequest
        {
            AccessLevel = accessLevel,
            Emails = emails,
            UserIds = userIds,
            ExpiresAt = parsedExpiresAt,
            MemberRoleId = memberRoleId
        };

        var invitation = await invitations.CreateForProjectAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToResult(invitation), "projects/:id/invitations (create)");
    }

    [McpServerTool(Name = "gitlab_create_resource_access_token", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a project access token (a bot-account credential scoped to one project) with chosen scopes. The plaintext token is returned only in this response; GitLab never discloses it again.")]
    public async Task<CallToolResult> CreateResourceAccessTokenAsync(
        [Description("Project: either the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string project,
        [Description("A display name for the token.")]
        string name,
        [Description(
            "At least one GitLab API scope, e.g. \"api\", \"read_api\", \"read_repository\", \"write_repository\".")]
        string[] scopes,
        [Description("Optional free-text note describing the token's purpose.")]
        string? description = null,
        [Description(
            "Optional expiry date, ISO 8601 (YYYY-MM-DD). GitLab defaults to 365 days from today when omitted.")]
        string? expiresAt = null,
        [Description(
            "Optional access level for the token's bot user: 10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner. Omit to leave GitLab's default.")]
        int? accessLevel = null,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Length == 0) throw new McpException("scopes must contain at least one scope.");

        var parsedExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt));

        var request = new CreateAccessTokenRequest
        {
            Name = name,
            Scopes = scopes,
            Description = description,
            ExpiresAt = parsedExpiresAt,
            AccessLevel = accessLevel
        };

        var token = await accessTokens.CreateForProjectAsync(project, request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToCreated(token), "projects/:id/access_tokens (create)");
    }

    private static DateOnly? ParseDateOnly(string? value, string paramName)
    {
        if (value is null) return null;

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed)) throw new McpException($"{paramName} must be an ISO 8601 date (YYYY-MM-DD).");

        return parsed;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value, string paramName)
    {
        if (value is null) return null;

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out var parsed)) throw new McpException($"{paramName} must be a valid ISO 8601 date-time.");

        return parsed;
    }

    [McpServerTool(Name = "gitlab_get_member", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one project or group member by numeric user id. Set includeInherited to also see access inherited from an ancestor group or an invited group, which the plain lookup omits.")]
    public async Task<CallToolResult> GetMemberAsync(
        [Description("The numeric id of the user whose membership to look up.")]
        long userId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description(
            "When true, also considers access inherited from an ancestor group or an invited group. Default false (direct membership only).")]
        bool includeInherited = false,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        var member = scope == "project"
            ? includeInherited
                ? await members.GetIncludingInheritedForProjectAsync(project!, userId, cancellationToken)
                : await members.GetAsync(project!, userId, cancellationToken)
            : includeInherited
                ? await members.GetIncludingInheritedForGroupAsync(group!, userId, cancellationToken)
                : await members.GetForGroupAsync(group!, userId, cancellationToken);

        var source = scope == "project" ? "projects/:id/members/:user_id" : "groups/:id/members/:user_id";
        return GitLabContent.Wrap(PeopleMapper.ToSummary(member), source);
    }

    [McpServerTool(Name = "gitlab_add_member", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds an existing GitLab user to a project or group at a given access level, addressed by numeric user id or by username.")]
    public async Task<CallToolResult> AddMemberAsync(
        [Description("The access level to grant: 10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner.")]
        int accessLevel,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description("The numeric id of the user to add. Provide this or username, not both.")]
        long? userId = null,
        [Description("The username of the user to add. Provide this or userId, not both.")]
        string? username = null,
        [Description("Optional expiry date for the membership, ISO 8601 (YYYY-MM-DD). Omit for no expiry.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        var hasUserId = userId is not null;
        var hasUsername = !string.IsNullOrWhiteSpace(username);
        if (hasUserId == hasUsername) throw new McpException("Provide exactly one of userId or username.");

        var parsedExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt));

        var member = scope == "project"
            ? await members.AddAsync(project!, new AddMemberRequest
            {
                AccessLevel = accessLevel,
                UserId = userId,
                Username = username,
                ExpiresAt = parsedExpiresAt
            }, cancellationToken)
            : await members.AddForGroupAsync(group!, new AddGroupMemberRequest
            {
                AccessLevel = accessLevel,
                UserId = userId,
                Username = username,
                ExpiresAt = parsedExpiresAt
            }, cancellationToken);

        var source = scope == "project" ? "projects/:id/members (add)" : "groups/:id/members (add)";
        return GitLabContent.Wrap(PeopleMapper.ToSummary(member), source);
    }

    [McpServerTool(Name = "gitlab_update_member", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a project or group member's access level, membership expiry, or attached custom member role.")]
    public async Task<CallToolResult> UpdateMemberAsync(
        [Description("The numeric id of the member to update.")]
        long userId,
        [Description("The new access level: 10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner.")]
        int accessLevel,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description("New expiry date, ISO 8601 (YYYY-MM-DD), or omit to leave unchanged.")]
        string? expiresAt = null,
        [Description("Numeric id of a custom member role to attach, or omit to leave unchanged.")]
        long? memberRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);
        var parsedExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt));

        var member = scope == "project"
            ? await members.UpdateAsync(project!, userId, new UpdateMemberRequest
            {
                AccessLevel = accessLevel,
                ExpiresAt = parsedExpiresAt,
                MemberRoleId = memberRoleId
            }, cancellationToken)
            : await members.UpdateForGroupAsync(group!, userId, new UpdateGroupMemberRequest
            {
                AccessLevel = accessLevel,
                ExpiresAt = parsedExpiresAt,
                MemberRoleId = memberRoleId
            }, cancellationToken);

        var source = scope == "project"
            ? "projects/:id/members/:user_id (update)"
            : "groups/:id/members/:user_id (update)";
        return GitLabContent.Wrap(PeopleMapper.ToSummary(member), source);
    }

    [McpServerTool(Name = "gitlab_remove_member", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes a direct member from a project or group. On a group, GitLab's default also removes the membership from every subgroup and project and frees a billable seat; the two switches narrow that.")]
    public async Task<MemberRemovalResult> RemoveMemberAsync(
        [Description("The numeric id of the member to remove.")]
        long userId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description(
            "Group scope only: when true, does not also remove the membership from subgroups and projects. Default false (GitLab's default: cascade).")]
        bool? skipSubresources = null,
        [Description(
            "Group scope only: when true, also unassigns the user from every issue and merge request in the group. Default false (GitLab's default: keep the user's assignments).")]
        bool? unassignIssuables = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (scope == "project")
        {
            await members.RemoveAsync(project!, userId, cancellationToken);
        }
        else
        {
            RemoveGroupMemberOptions? options = skipSubresources is null && unassignIssuables is null
                ? null
                : new RemoveGroupMemberOptions
                    { SkipSubresources = skipSubresources, UnassignIssuables = unassignIssuables };

            await members.RemoveForGroupAsync(group!, userId, options, cancellationToken);
        }

        return new MemberRemovalResult(userId, scope);
    }

    [McpServerTool(Name = "gitlab_list_pending_group_members", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists a top-level group's memberships still awaiting approval, including people invited by email who have no GitLab account yet.")]
    public async Task<CallToolResult> ListPendingGroupMembersAsync(
        [Description("The top-level group's numeric id or full path.")]
        string group,
        [Description("Maximum members to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<PendingGroupMemberSummary> collected = [];
        var truncated = false;

        await foreach (var pending in members.ListPendingForGroupAsync(group, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(pending));
        }

        return GitLabContent.Wrap(new PendingGroupMemberListResult(collected, truncated), "groups/:id/pending_members");
    }

    [McpServerTool(Name = "gitlab_approve_pending_group_members", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Approves a group's pending memberships: one membership by its numeric membership id, or every pending membership at once when membershipId is omitted.")]
    public async Task<ApprovePendingGroupMembersResult> ApprovePendingGroupMembersAsync(
        [Description("The top-level group's numeric id or full path.")]
        string group,
        [Description("The numeric membership id to approve. Omit to approve every pending membership in the group.")]
        long? membershipId = null,
        CancellationToken cancellationToken = default)
    {
        if (membershipId is { } id)
        {
            await members.ApproveForGroupAsync(group, id, cancellationToken);
            return new ApprovePendingGroupMembersResult(id, false);
        }

        await members.ApproveAllForGroupAsync(group, cancellationToken);
        return new ApprovePendingGroupMembersResult(null, true);
    }

    [McpServerTool(Name = "gitlab_set_group_member_ldap_override", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Flags or clears a group member's LDAP-sync override. While flagged, the next LDAP synchronisation leaves that member's access level alone instead of resetting it.")]
    public async Task<CallToolResult> SetGroupMemberLdapOverrideAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The numeric id of the member to change.")]
        long userId,
        [Description(
            "true to flag the override so LDAP sync will not touch this member; false to clear it so LDAP sync resumes control.")]
        bool overrideLdap,
        CancellationToken cancellationToken = default)
    {
        GitLabMember member;
        if (overrideLdap)
        {
            member = await members.SetOverrideForGroupAsync(group, userId, cancellationToken);
        }
        else
        {
            await members.RemoveOverrideForGroupAsync(group, userId, cancellationToken);
            member = await members.GetForGroupAsync(group, userId, cancellationToken);
        }

        return GitLabContent.Wrap(PeopleMapper.ToSummary(member), "groups/:id/members/:user_id/override");
    }

    [McpServerTool(Name = "gitlab_create_member_role", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a custom member role that layers chosen abilities on a base access level. scope decides where it is created: \"instance\" (self-managed, instance-wide, Ultimate tier), \"group\" (one top-level group), or \"admin\" (an administrator role with its own smaller set of \"read_admin_*\" abilities and no base access level).")]
    public async Task<CallToolResult> CreateMemberRoleAsync(
        [Description("Where the role is created: \"instance\", \"group\", or \"admin\".")]
        string scope,
        [Description("A display name for the role.")]
        string name,
        [Description(
            "Required for \"instance\" and \"group\" scope: the access level the role builds on (10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner). Not used for \"admin\" scope.")]
        int? baseAccessLevel = null,
        [Description("Required for \"group\" scope: the group's numeric id or full path.")]
        string? group = null,
        [Description("Optional free-text description of the role.")]
        string? description = null,
        [Description(
            "Abilities to enable, e.g. \"read_code\", \"admin_runners\" (see gitlab_list_member_roles for the full vocabulary). For \"admin\" scope only the six \"read_admin_*\" abilities are valid. Omit for a role with no abilities beyond the base access level.")]
        string[]? abilities = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = scope.ToLowerInvariant();
        if (normalizedScope is not ("instance" or "group" or "admin"))
            throw new McpException("scope must be \"instance\", \"group\", or \"admin\".");

        if (normalizedScope == "group" && string.IsNullOrWhiteSpace(group))
            throw new McpException("group is required when scope is \"group\".");

        if (normalizedScope != "admin" && baseAccessLevel is null)
            throw new McpException("baseAccessLevel is required when scope is \"instance\" or \"group\".");

        GitLabMemberRole role;
        if (normalizedScope == "admin")
        {
            var request = new CreateAdminMemberRoleRequest { Name = name, Description = description };
            foreach (var ability in abilities ?? []) request = ApplyAdminAbility(request, ability);

            role = await memberRoles.CreateAdminRoleAsync(request, cancellationToken);
        }
        else
        {
            var request = new CreateMemberRoleRequest
                { Name = name, Description = description, BaseAccessLevel = baseAccessLevel };
            foreach (var ability in abilities ?? []) request = ApplyAbility(request, ability);

            role = normalizedScope == "group"
                ? await memberRoles.CreateForGroupAsync(group!, request, cancellationToken)
                : await memberRoles.CreateAsync(request, cancellationToken);
        }

        return GitLabContent.Wrap(PeopleMapper.ToSummary(role), "member_roles (create)");
    }

    [McpServerTool(Name = "gitlab_delete_member_role", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a custom member role by its numeric id. scope must match where the role was created: \"instance\", \"group\" (also requires group), or \"admin\".")]
    public async Task<MemberRoleDeletionResult> DeleteMemberRoleAsync(
        [Description("The numeric id of the member role to delete.")]
        long memberRoleId,
        [Description("Where the role lives: \"instance\", \"group\", or \"admin\".")]
        string scope,
        [Description("Required when scope is \"group\": the group's numeric id or full path.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = scope.ToLowerInvariant();

        switch (normalizedScope)
        {
            case "instance":
                await memberRoles.DeleteAsync(memberRoleId, cancellationToken);
                break;

            case "group":
                if (string.IsNullOrWhiteSpace(group))
                    throw new McpException("group is required when scope is \"group\".");

                await memberRoles.DeleteForGroupAsync(group, memberRoleId, cancellationToken);
                break;

            case "admin":
                await memberRoles.DeleteAdminRoleAsync(memberRoleId, cancellationToken);
                break;

            default:
                throw new McpException("scope must be \"instance\", \"group\", or \"admin\".");
        }

        return new MemberRoleDeletionResult(memberRoleId, normalizedScope);
    }

    [McpServerTool(Name = "gitlab_request_access", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Requests access to a project or group as the authenticated user. The request stays pending until a maintainer or owner reviews it.")]
    public async Task<CallToolResult> RequestAccessAsync(
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        var request = scope == "project"
            ? await accessRequests.RequestForProjectAsync(project!, cancellationToken)
            : await accessRequests.RequestForGroupAsync(group!, cancellationToken);

        var source = scope == "project"
            ? "projects/:id/access_requests (create)"
            : "groups/:id/access_requests (create)";
        return GitLabContent.Wrap(PeopleMapper.ToSummary(request), source);
    }

    [McpServerTool(Name = "gitlab_review_access_request", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Approves a pending access request at a chosen access level, turning the requester into a member, or denies it outright.")]
    public async Task<CallToolResult> ReviewAccessRequestAsync(
        [Description("The numeric id of the user whose access request to review.")]
        long userId,
        [Description("The decision: \"approve\" or \"deny\".")]
        string decision,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description(
            "Required when decision is \"approve\": the access level to grant (10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner).")]
        int? accessLevel = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);
        var normalizedDecision = decision.ToLowerInvariant();

        if (normalizedDecision is not ("approve" or "deny"))
            throw new McpException("decision must be \"approve\" or \"deny\".");

        if (normalizedDecision == "approve" && accessLevel is null)
            throw new McpException("accessLevel is required when decision is \"approve\".");

        MemberSummary? memberSummary = null;
        if (normalizedDecision == "approve")
        {
            var approveRequest = new ApproveAccessRequestRequest { AccessLevel = accessLevel };
            var member = scope == "project"
                ? await accessRequests.ApproveForProjectAsync(project!, userId, approveRequest, cancellationToken)
                : await accessRequests.ApproveForGroupAsync(group!, userId, approveRequest, cancellationToken);
            memberSummary = PeopleMapper.ToSummary(member);
        }
        else if (scope == "project")
        {
            await accessRequests.DenyForProjectAsync(project!, userId, cancellationToken);
        }
        else
        {
            await accessRequests.DenyForGroupAsync(group!, userId, cancellationToken);
        }

        var source = scope == "project"
            ? "projects/:id/access_requests/:user_id (review)"
            : "groups/:id/access_requests/:user_id (review)";
        return GitLabContent.Wrap(new AccessRequestReviewResult(userId, normalizedDecision, memberSummary), source);
    }

    [McpServerTool(Name = "gitlab_list_invitations", ReadOnly = true, OpenWorld = false)]
    [Description("Lists a project's or group's pending, directly-made invitations (not access requests).")]
    public async Task<CallToolResult> ListInvitationsAsync(
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description("Optional search over the invited email or name.")]
        string? search = null,
        [Description("Maximum invitations to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new InvitationListOptions { Query = search, PerPage = Math.Min(limit + 1, MaxLimit) };

        var stream = scope == "project"
            ? invitations.ListForProjectAsync(project!, options, cancellationToken)
            : invitations.ListForGroupAsync(group!, options, cancellationToken);

        List<InvitationResult> collected = [];
        var truncated = false;

        await foreach (var invitation in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToResult(invitation));
        }

        var source = scope == "project" ? "projects/:id/invitations" : "groups/:id/invitations";
        return GitLabContent.Wrap(new InvitationListResult(collected, truncated), source);
    }

    [McpServerTool(Name = "gitlab_update_invitation", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Changes a pending project or group invitation's access level, expiry, or member role. Addressed by the invited email, not an id.")]
    public async Task<CallToolResult> UpdateInvitationAsync(
        [Description("The invited email address.")]
        string email,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description(
            "New access level, or omit to leave unchanged: 10=Guest, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner.")]
        int? accessLevel = null,
        [Description("New expiry, ISO 8601 date-time, or omit to leave unchanged.")]
        string? expiresAt = null,
        [Description("New numeric custom member role id, or omit to leave unchanged.")]
        long? memberRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);
        var parsedExpiresAt = ParseDateTimeOffset(expiresAt, nameof(expiresAt));

        var request = new UpdateInvitationRequest
        {
            AccessLevel = accessLevel,
            ExpiresAt = parsedExpiresAt,
            MemberRoleId = memberRoleId
        };

        var invitation = scope == "project"
            ? await invitations.UpdateForProjectAsync(project!, email, request, cancellationToken)
            : await invitations.UpdateForGroupAsync(group!, email, request, cancellationToken);

        var source = scope == "project"
            ? "projects/:id/invitations/:email (update)"
            : "groups/:id/invitations/:email (update)";
        return GitLabContent.Wrap(PeopleMapper.ToResult(invitation), source);
    }

    [McpServerTool(Name = "gitlab_delete_invitation", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Revokes a pending project or group invitation, addressed by the invited email address.")]
    public async Task<InvitationDeletionResult> DeleteInvitationAsync(
        [Description("The invited email address.")]
        string email,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (scope == "project")
            await invitations.DeleteForProjectAsync(project!, email, cancellationToken);
        else
            await invitations.DeleteForGroupAsync(group!, email, cancellationToken);

        return new InvitationDeletionResult(email);
    }

    [McpServerTool(Name = "gitlab_list_saml_group_links", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists every SAML group link configured on a group, or gets just one by SAML group name. Ultimate feature on a top-level group.")]
    public async Task<CallToolResult> ListSamlGroupLinksAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The SAML group name to get a single link for. Omit to list every link on the group.")]
        string? samlGroupName = null,
        [Description(
            "The SAML provider, needed only when samlGroupName is given and the instance has more than one configured SAML provider.")]
        string? provider = null,
        [Description("Maximum links to return when listing (1-100). Default 20. Ignored when samlGroupName is given.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (samlGroupName is not null)
        {
            var link = await samlGroupLinks.GetAsync(group, samlGroupName, provider ?? string.Empty, cancellationToken);
            return GitLabContent.Wrap(
                new SamlGroupLinkListResult([PeopleMapper.ToSummary(link)], false),
                "groups/:id/saml_group_links/:saml_group_name");
        }

        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        List<SamlGroupLinkSummary> collected = [];
        var truncated = false;

        await foreach (var link in samlGroupLinks.ListAsync(group, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(link));
        }

        return GitLabContent.Wrap(new SamlGroupLinkListResult(collected, truncated), "groups/:id/saml_group_links");
    }

    [McpServerTool(Name = "gitlab_create_saml_group_link", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Grants members of a named SAML group a role in a GitLab group. Ultimate feature on a top-level group.")]
    public async Task<CallToolResult> CreateSamlGroupLinkAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The SAML group name to link, exactly as it appears in the SAML assertion.")]
        string samlGroupName,
        [Description(
            "The role to grant: 5=Minimal Access, 10=Guest, 15=Planner, 20=Reporter, 30=Developer, 40=Maintainer, 50=Owner.")]
        int accessLevel,
        [Description("Optional numeric id of a custom member role to grant alongside accessLevel.")]
        long? memberRoleId = null,
        [Description(
            "The SAML provider this link applies to. Required only on instances with more than one configured SAML provider.")]
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateSamlGroupLinkRequest
        {
            SamlGroupName = samlGroupName,
            AccessLevel = accessLevel,
            MemberRoleId = memberRoleId,
            Provider = provider
        };

        var link = await samlGroupLinks.CreateAsync(group, request, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToSummary(link), "groups/:id/saml_group_links (create)");
    }

    [McpServerTool(Name = "gitlab_delete_saml_group_link", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes a SAML group link. Existing members keep their access until their next SAML sign-in, when the link no longer re-grants it.")]
    public async Task<SamlGroupLinkDeletionResult> DeleteSamlGroupLinkAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The SAML group name of the link to delete.")]
        string samlGroupName,
        [Description("The SAML provider, needed only when the instance has more than one configured SAML provider.")]
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        await samlGroupLinks.DeleteAsync(group, samlGroupName, provider ?? string.Empty, cancellationToken);
        return new SamlGroupLinkDeletionResult(samlGroupName, provider);
    }

    [McpServerTool(Name = "gitlab_list_group_provider_identities", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the SAML or SCIM identities linked to a group's members, or gets just one by its external UID.")]
    public async Task<CallToolResult> ListGroupProviderIdentitiesAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("Which identity kind to read: \"saml\" or \"scim\".")]
        string kind,
        [Description("The external UID to get a single identity for. Omit to list every identity of the chosen kind.")]
        string? externUid = null,
        [Description("Maximum identities to return when listing (1-100). Default 20. Ignored when externUid is given.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = kind.ToLowerInvariant();
        if (normalizedKind is not ("saml" or "scim")) throw new McpException("kind must be \"saml\" or \"scim\".");

        if (externUid is not null)
        {
            var identity = normalizedKind == "saml"
                ? await providerIdentities.GetSamlAsync(group, externUid, cancellationToken)
                : await providerIdentities.GetScimAsync(group, externUid, cancellationToken);

            return GitLabContent.Wrap(
                new ProviderIdentityListResult([PeopleMapper.ToSummary(identity)], false),
                normalizedKind == "saml"
                    ? "groups/:id/provider_identities/saml/:extern_uid"
                    : "groups/:id/provider_identities/scim/:extern_uid");
        }

        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var stream = normalizedKind == "saml"
            ? providerIdentities.ListSamlAsync(group, cancellationToken)
            : providerIdentities.ListScimAsync(group, cancellationToken);

        List<ProviderIdentitySummary> collected = [];
        var truncated = false;

        await foreach (var identity in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(identity));
        }

        var source = normalizedKind == "saml"
            ? "groups/:id/provider_identities/saml"
            : "groups/:id/provider_identities/scim";
        return GitLabContent.Wrap(new ProviderIdentityListResult(collected, truncated), source);
    }

    // ---- backlog-split/people/part-2.json below ----

    private static string RequireNotificationScope(string scope, string? project, string? group)
    {
        var normalized = scope.ToLowerInvariant();

        switch (normalized)
        {
            case "global":
                return normalized;

            case "project":
                if (string.IsNullOrWhiteSpace(project))
                    throw new McpException("project is required when scope is \"project\".");

                return normalized;

            case "group":
                if (string.IsNullOrWhiteSpace(group))
                    throw new McpException("group is required when scope is \"group\".");

                return normalized;

            default:
                throw new McpException("scope must be \"global\", \"group\", or \"project\".");
        }
    }

    private static GitLabNotificationLevel ParseNotificationLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "disabled" => GitLabNotificationLevel.Disabled,
            "participating" => GitLabNotificationLevel.Participating,
            "watch" => GitLabNotificationLevel.Watch,
            "global" => GitLabNotificationLevel.Global,
            "mention" => GitLabNotificationLevel.Mention,
            "custom" => GitLabNotificationLevel.Custom,
            _ => throw new McpException(
                "level must be one of: disabled, participating, watch, global, mention, custom.")
        };
    }

    private static UpdateNotificationSettingsRequest ApplyNotificationEvents(UpdateNotificationSettingsRequest request,
        string[]? enable, string[]? disable)
    {
        foreach (var name in enable ?? [])
        {
            if (!NotificationEventSetters.TryGetValue(name, out var setter))
                throw new McpException($"Unknown notification event \"{name}\".");

            request = setter(request, true);
        }

        foreach (var name in disable ?? [])
        {
            if (!NotificationEventSetters.TryGetValue(name, out var setter))
                throw new McpException($"Unknown notification event \"{name}\".");

            request = setter(request, false);
        }

        return request;
    }

    [McpServerTool(Name = "gitlab_get_notification_settings", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets the caller's notification level and per-event email flags, either globally or for one group or project the caller belongs to.")]
    public async Task<CallToolResult> GetNotificationSettingsAsync(
        [Description("Which settings to read: \"global\", \"group\", or \"project\".")]
        string scope,
        [Description(
            "Required when scope is \"project\": the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string? project = null,
        [Description("Required when scope is \"group\": the numeric group id or its full path.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = RequireNotificationScope(scope, project, group);

        var settings = normalized switch
        {
            "project" => await notificationSettings.GetForProjectAsync(project!, cancellationToken),
            "group" => await notificationSettings.GetForGroupAsync(group!, cancellationToken),
            _ => await notificationSettings.GetGlobalAsync(cancellationToken)
        };

        var source = normalized switch
        {
            "project" => "projects/:id/notification_settings",
            "group" => "groups/:id/notification_settings",
            _ => "notification_settings"
        };

        return GitLabContent.Wrap(PeopleMapper.ToResult(settings), source);
    }

    [McpServerTool(Name = "gitlab_update_notification_settings", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description(
        "Updates the caller's notification level and/or per-event email flags, either globally or for one group or project. Only the fields provided are changed. Per-event flags only take effect once level is \"custom\".")]
    public async Task<CallToolResult> UpdateNotificationSettingsAsync(
        [Description("Which settings to change: \"global\", \"group\", or \"project\".")]
        string scope,
        [Description(
            "Required when scope is \"project\": the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string? project = null,
        [Description("Required when scope is \"group\": the numeric group id or its full path.")]
        string? group = null,
        [Description(
            "New notification level, or omit to leave unchanged: \"disabled\", \"participating\", \"watch\", \"global\", \"mention\", or \"custom\".")]
        string? level = null,
        [Description("New notification email address, or omit to leave unchanged.")]
        string? notificationEmail = null,
        [Description("Event names to turn on. Vocabulary: " + NotificationEventVocabulary + ".")]
        string[]? enableEvents = null,
        [Description("Event names to turn off, from the same vocabulary as enableEvents.")]
        string[]? disableEvents = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = RequireNotificationScope(scope, project, group);

        var request = new UpdateNotificationSettingsRequest
        {
            Level = level is null ? null : ParseNotificationLevel(level),
            NotificationEmail = notificationEmail
        };
        request = ApplyNotificationEvents(request, enableEvents, disableEvents);

        var settings = normalized switch
        {
            "project" => await notificationSettings.UpdateForProjectAsync(project!, request, cancellationToken),
            "group" => await notificationSettings.UpdateForGroupAsync(group!, request, cancellationToken),
            _ => await notificationSettings.UpdateGlobalAsync(request, cancellationToken)
        };

        var source = normalized switch
        {
            "project" => "projects/:id/notification_settings (update)",
            "group" => "groups/:id/notification_settings (update)",
            _ => "notification_settings (update)"
        };

        return GitLabContent.Wrap(PeopleMapper.ToResult(settings), source);
    }

    [McpServerTool(Name = "gitlab_get_current_user", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets the account the configured token authenticates as, optionally with its badge counters (assigned issues, assigned/review-requested merge requests, pending todos).")]
    public async Task<CallToolResult> GetCurrentUserAsync(
        [Description("When true, also fetches the caller's badge counters. Default false.")]
        bool includeCounts = false,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetCurrentAsync(cancellationToken);
        var counts = includeCounts ? PeopleMapper.ToSummary(await users.GetCountsAsync(cancellationToken)) : null;

        return GitLabContent.Wrap(new CurrentUserResult(PeopleMapper.ToSummary(user), counts), "user");
    }

    [McpServerTool(Name = "gitlab_manage_current_user_email", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Lists, adds, or deletes the caller's own secondary email addresses. The primary address is not managed here.")]
    public async Task<CallToolResult> ManageCurrentUserEmailAsync(
        [Description("The operation: \"list\", \"add\", or \"delete\".")]
        string action,
        [Description(
            "Required when action is \"add\": the email address to add. GitLab sends a confirmation mail before it is used for notifications.")]
        string? email = null,
        [Description(
            "Required when action is \"delete\": the numeric id of the secondary email to remove (the email's own id, not the user's).")]
        long? emailId = null,
        [Description("Maximum emails to return when action is \"list\" (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        switch (normalized)
        {
            case "list":
            {
                if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

                List<EmailSummary> collected = [];
                var truncated = false;

                await foreach (var item in currentUser.ListEmailsAsync(cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(PeopleMapper.ToSummary(item));
                }

                return GitLabContent.Wrap(new CurrentUserEmailResult("list", collected, truncated, null, null),
                    "user/emails");
            }

            case "add":
            {
                if (string.IsNullOrWhiteSpace(email))
                    throw new McpException("email is required when action is \"add\".");

                var added = await currentUser.AddEmailAsync(new AddCurrentUserEmailRequest { Email = email },
                    cancellationToken);
                return GitLabContent.Wrap(
                    new CurrentUserEmailResult("add", null, false, PeopleMapper.ToSummary(added), null),
                    "user/emails (add)");
            }

            case "delete":
            {
                if (emailId is null) throw new McpException("emailId is required when action is \"delete\".");

                await currentUser.DeleteEmailAsync(emailId.Value, cancellationToken);
                return GitLabContent.Wrap(new CurrentUserEmailResult("delete", null, false, null, emailId),
                    "user/emails/:id (delete)");
            }

            default:
                throw new McpException("action must be \"list\", \"add\", or \"delete\".");
        }
    }

    /// <summary>
    ///     Every field of <see cref="UserPreferencesResult" /> is a bool preference flag GitLab returns as a
    ///     plain member, not free text, so this is the rare bare-record (unwrapped) case (CLAUDE.md rule 4).
    /// </summary>
    [McpServerTool(Name = "gitlab_manage_current_user_preferences", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false)]
    [Description("Gets or updates the caller's diff-viewing and CI-identity preference flags.")]
    public async Task<UserPreferencesResult> ManageCurrentUserPreferencesAsync(
        [Description("The operation: \"get\" or \"update\".")]
        string action,
        [Description("Update only: show each changed file as its own diff view. Omit to leave unchanged.")]
        bool? viewDiffsFileByFile = null,
        [Description("Update only: render whitespace-only changes in diffs. Omit to leave unchanged.")]
        bool? showWhitespaceInDiffs = null,
        [Description(
            "Update only: include the caller's linked identities in CI/CD JWT tokens. Omit to leave unchanged.")]
        bool? passUserIdentitiesToCiJwt = null,
        [Description("Update only: use the advanced policy editor. Omit to leave unchanged.")]
        bool? policyAdvancedEditor = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        GitLabUserPreferences preferences;
        switch (normalized)
        {
            case "get":
                preferences = await currentUser.GetPreferencesAsync(cancellationToken);
                break;

            case "update":
            {
                var request = new UpdateUserPreferencesRequest
                {
                    ViewDiffsFileByFile = viewDiffsFileByFile,
                    ShowWhitespaceInDiffs = showWhitespaceInDiffs,
                    PassUserIdentitiesToCiJwt = passUserIdentitiesToCiJwt,
                    PolicyAdvancedEditor = policyAdvancedEditor
                };
                preferences = await currentUser.UpdatePreferencesAsync(request, cancellationToken);
                break;
            }

            default:
                throw new McpException("action must be \"get\" or \"update\".");
        }

        return PeopleMapper.ToResult(preferences);
    }

    private static GitLabUserStatusClearAfter? ParseClearStatusAfter(string? value)
    {
        if (value is null) return null;

        if (!Enum.TryParse<GitLabUserStatusClearAfter>(value, true, out var parsed))
            throw new McpException(
                "clearStatusAfter must be one of: ThirtyMinutes, ThreeHours, EightHours, OneDay, ThreeDays, SevenDays, ThirtyDays.");

        return parsed;
    }

    [McpServerTool(Name = "gitlab_manage_current_user_status", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description(
        "Gets, replaces, or amends the caller's profile status. \"replace\" clears every field the call omits; \"amend\" leaves omitted fields as they are.")]
    public async Task<CallToolResult> ManageCurrentUserStatusAsync(
        [Description("The operation: \"get\", \"replace\", or \"amend\".")]
        string action,
        [Description("Replace/amend only: an emoji short name, e.g. \"speech_balloon\".")]
        string? emoji = null,
        [Description("Replace/amend only: the status message text.")]
        string? message = null,
        [Description("Replace/amend only: availability, e.g. \"busy\" or \"not_set\".")]
        string? availability = null,
        [Description(
            "Replace/amend only: automatically clear the status after this period: \"ThirtyMinutes\", \"ThreeHours\", \"EightHours\", \"OneDay\", \"ThreeDays\", \"SevenDays\", or \"ThirtyDays\". Omit for no auto-clear.")]
        string? clearStatusAfter = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        GitLabUserStatus status;
        switch (normalized)
        {
            case "get":
                status = await currentUser.GetStatusAsync(cancellationToken);
                break;

            case "replace":
            case "amend":
            {
                var request = new SetUserStatusRequest
                {
                    Emoji = emoji,
                    Message = message,
                    Availability = availability,
                    ClearStatusAfter = ParseClearStatusAfter(clearStatusAfter)
                };

                status = normalized == "replace"
                    ? await currentUser.SetStatusAsync(request, cancellationToken)
                    : await currentUser.UpdateStatusAsync(request, cancellationToken);
                break;
            }

            default:
                throw new McpException("action must be \"get\", \"replace\", or \"amend\".");
        }

        return GitLabContent.Wrap(PeopleMapper.ToResult(status), "user/status");
    }

    [McpServerTool(Name = "gitlab_manage_current_user_support_pin", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Gets the caller's current GitLab Support PIN if one exists and has not expired, or creates a new one valid for seven days (superseding any existing PIN). GitLab Support asks for it to verify identity; treat it as a secret.")]
    public async Task<CallToolResult> ManageCurrentUserSupportPinAsync(
        [Description("The operation: \"get\" or \"create\".")]
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        var pin = normalized switch
        {
            "get" => await currentUser.GetSupportPinAsync(cancellationToken),
            "create" => await currentUser.CreateSupportPinAsync(cancellationToken),
            _ => throw new McpException("action must be \"get\" or \"create\".")
        };

        return GitLabContent.Wrap(PeopleMapper.ToResult(pin), "user/support_pin");
    }

    [McpServerTool(Name = "gitlab_get_user", ReadOnly = true, OpenWorld = false)]
    [Description("Looks up any user by numeric id.")]
    public async Task<CallToolResult> GetUserAsync(
        [Description("The numeric id of the user to look up.")]
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(userId, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToSummary(user), "users/:id");
    }

    [McpServerTool(Name = "gitlab_list_users", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Searches or filters users visible to the caller. blocked and admins are honoured for administrators only; a non-administrator token still gets a result back, just without those two filters applied.")]
    public async Task<CallToolResult> ListUsersAsync(
        [Description("Optional free-text search over name/username/email.")]
        string? search = null,
        [Description("Optional exact username filter.")]
        string? username = null,
        [Description(
            "Optional filter: true for active accounts only, false for blocked/deactivated only. Omit for both.")]
        bool? active = null,
        [Description("Optional filter: true for external users only, false for internal only. Omit for both.")]
        bool? external = null,
        [Description("Administrators only: true to return blocked accounts only.")]
        bool? blocked = null,
        [Description("Administrators only: true to return instance administrators only.")]
        bool? admins = null,
        [Description("Maximum users to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new UserListOptions
        {
            Search = search,
            Username = username,
            Active = active,
            External = external,
            Blocked = blocked,
            Admins = admins,
            PerPage = Math.Min(limit + 1, MaxLimit)
        };

        List<EnterpriseUserSummary> collected = [];
        var truncated = false;

        await foreach (var user in users.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(user));
        }

        return GitLabContent.Wrap(new UserListResult(collected, truncated), "users");
    }

    [McpServerTool(Name = "gitlab_manage_user", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a new user account, or updates an existing one by numeric id. Administrators only. Update is a partial update: fields left unset are unchanged.")]
    public async Task<CallToolResult> ManageUserAsync(
        [Description("The operation: \"create\" or \"update\".")]
        string mode,
        [Description("Required for \"update\": the numeric id of the user to change.")]
        long? userId = null,
        [Description("Required for \"create\": the account's email address.")]
        string? email = null,
        [Description("Required for \"create\": the account's username.")]
        string? username = null,
        [Description("Required for \"create\": the account's display name.")]
        string? name = null,
        [Description(
            "Create only: when true, GitLab emails the new user a password-reset link instead of a password being set here. Recommended over setting a password directly.")]
        bool? resetPassword = null,
        [Description(
            "Create only: when true, the account is created already confirmed, skipping the confirmation email.")]
        bool? skipConfirmation = null,
        [Description(
            "Grants or revokes instance administrator rights. Omit to leave unchanged (update) or use GitLab's default (create).")]
        bool? isAdmin = null,
        [Description("Whether the user may create top-level groups. Omit to leave unchanged/default.")]
        bool? canCreateGroup = null,
        [Description(
            "Marks the account as external (restricted from internal-only projects). Omit to leave unchanged/default.")]
        bool? external = null,
        [Description("Personal-project limit. Omit to leave unchanged/default.")]
        int? projectsLimit = null,
        [Description(
            "Publicly-visible email address, distinct from the account's login email. Omit to leave unchanged.")]
        string? publicEmail = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = mode.ToLowerInvariant();

        GitLabUser user;
        switch (normalized)
        {
            case "create":
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(name))
                    throw new McpException("email, username, and name are all required when mode is \"create\".");

                var request = new CreateUserRequest
                {
                    Email = email,
                    Username = username,
                    Name = name,
                    ResetPassword = resetPassword,
                    SkipConfirmation = skipConfirmation,
                    Admin = isAdmin,
                    CanCreateGroup = canCreateGroup,
                    External = external,
                    ProjectsLimit = projectsLimit,
                    PublicEmail = publicEmail
                };

                user = await users.CreateAsync(request, cancellationToken);
                break;
            }

            case "update":
            {
                if (userId is null) throw new McpException("userId is required when mode is \"update\".");

                var request = new UpdateUserRequest
                {
                    Email = email,
                    Username = username,
                    Name = name,
                    Admin = isAdmin,
                    CanCreateGroup = canCreateGroup,
                    External = external,
                    ProjectsLimit = projectsLimit,
                    PublicEmail = publicEmail
                };

                user = await users.UpdateAsync(userId.Value, request, cancellationToken);
                break;
            }

            default:
                throw new McpException("mode must be \"create\" or \"update\".");
        }

        return GitLabContent.Wrap(PeopleMapper.ToManageResult(user), "users (manage)");
    }

    /// <summary>
    ///     GitLab's delete-user endpoint returns no body; both fields are the caller's own input, so this is
    ///     the rare bare-record case.
    /// </summary>
    [McpServerTool(Name = "gitlab_delete_user", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a user account. Administrators only. hardDelete additionally deletes the user's contributions (instead of moving them to the Ghost User) and groups they solely own, and cannot be undone -- check associations first if unsure.")]
    public async Task<UserDeletionResult> DeleteUserAsync(
        [Description("The numeric id of the user to delete.")]
        long userId,
        [Description(
            "When true, also deletes the user's contributions and solely-owned groups. Not reversible. Default false.")]
        bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        await users.DeleteAsync(userId, hardDelete, cancellationToken);
        return new UserDeletionResult(userId, hardDelete);
    }

    /// <summary>
    ///     Every one of the nine lifecycle endpoints this dispatches to returns no body; both result fields
    ///     are the caller's own input, so this is the rare bare-record case.
    /// </summary>
    [McpServerTool(Name = "gitlab_set_user_account_state", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Moves a user account through an administrative lifecycle action: activate, deactivate, block, unblock, ban, unban, approve a pending sign-up, reject one, or disable two-factor authentication. Administrators only.")]
    public async Task<UserAccountStateResult> SetUserAccountStateAsync(
        [Description("The numeric id of the user to change.")]
        long userId,
        [Description(
            "The action: \"activate\", \"deactivate\", \"block\", \"unblock\", \"ban\", \"unban\", \"approve\", \"reject\", or \"disable_two_factor\".")]
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        var task = normalized switch
        {
            "activate" => users.ActivateAsync(userId, cancellationToken),
            "deactivate" => users.DeactivateAsync(userId, cancellationToken),
            "block" => users.BlockAsync(userId, cancellationToken),
            "unblock" => users.UnblockAsync(userId, cancellationToken),
            "ban" => users.BanAsync(userId, cancellationToken),
            "unban" => users.UnbanAsync(userId, cancellationToken),
            "approve" => users.ApproveAsync(userId, cancellationToken),
            "reject" => users.RejectAsync(userId, cancellationToken),
            "disable_two_factor" => users.DisableTwoFactorAsync(userId, cancellationToken),
            _ => throw new McpException(
                "action must be one of: activate, deactivate, block, unblock, ban, unban, approve, reject, disable_two_factor.")
        };

        await task;
        return new UserAccountStateResult(userId, normalized);
    }

    [McpServerTool(Name = "gitlab_set_user_follow", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false)]
    [Description("Follows or unfollows another user as the caller.")]
    public async Task<CallToolResult> SetUserFollowAsync(
        [Description("The numeric id of the user to follow or unfollow.")]
        long userId,
        [Description("The action: \"follow\" or \"unfollow\".")]
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalized = action.ToLowerInvariant();

        var user = normalized switch
        {
            "follow" => await users.FollowAsync(userId, cancellationToken),
            "unfollow" => await users.UnfollowAsync(userId, cancellationToken),
            _ => throw new McpException("action must be \"follow\" or \"unfollow\".")
        };

        return GitLabContent.Wrap(PeopleMapper.ToSummary(user),
            normalized == "follow" ? "users/:id/follow" : "users/:id/unfollow");
    }

    [McpServerTool(Name = "gitlab_get_user_status", ReadOnly = true, OpenWorld = false)]
    [Description("Gets a user's profile status (emoji, message, availability) by numeric id or username.")]
    public async Task<CallToolResult> GetUserStatusAsync(
        [Description("The numeric id or the username of the user.")]
        string userIdOrUsername,
        CancellationToken cancellationToken = default)
    {
        var status = await users.GetStatusAsync(userIdOrUsername, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToResult(status), "users/:id/status");
    }

    [McpServerTool(Name = "gitlab_list_personal_access_tokens", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists personal access tokens, never their plaintext secret. Administrators see every token on the instance; everyone else sees only their own.")]
    public async Task<CallToolResult> ListPersonalAccessTokensAsync(
        [Description("Administrators only: restrict the list to this user's tokens.")]
        long? userId = null,
        [Description("Optional state filter: \"active\" or \"inactive\". Omit for all.")]
        string? state = null,
        [Description("Optional free-text search over the token's name.")]
        string? search = null,
        [Description("Maximum tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new PersonalAccessTokenListOptions
            { UserId = userId, State = state, Search = search, PerPage = Math.Min(limit + 1, MaxLimit) };

        List<PersonalAccessTokenSummary> collected = [];
        var truncated = false;

        await foreach (var token in personalAccessTokens.ListAsync(options, cancellationToken))
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToResult(token));
        }

        return GitLabContent.Wrap(new PersonalAccessTokenListResult(collected, truncated), "personal_access_tokens");
    }

    [McpServerTool(Name = "gitlab_get_personal_access_token", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one personal access token by numeric id, or the token that authenticated this call when tokenId is omitted. Never returns the plaintext secret.")]
    public async Task<CallToolResult> GetPersonalAccessTokenAsync(
        [Description(
            "The numeric id of the token to look up. Omit to get the token that authenticated this call (only meaningful when this server is configured with a personal access token).")]
        long? tokenId = null,
        CancellationToken cancellationToken = default)
    {
        var token = tokenId is { } id
            ? await personalAccessTokens.GetAsync(id, cancellationToken)
            : await personalAccessTokens.GetSelfAsync(cancellationToken);

        return GitLabContent.Wrap(PeopleMapper.ToResult(token),
            tokenId is null ? "personal_access_tokens/self" : "personal_access_tokens/:id");
    }

    [McpServerTool(Name = "gitlab_create_personal_access_token", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates a personal access token for the caller, or, as an administrator, for another user by userId. Creating for the caller is restricted by GitLab to the \"k8s_proxy\" and \"self_rotate\" scopes; creating for another user accepts any scope. The plaintext token is returned only in this response.")]
    public async Task<CallToolResult> CreatePersonalAccessTokenAsync(
        [Description("A display name for the token.")]
        string name,
        [Description(
            "At least one GitLab API scope. For the caller's own token, only \"k8s_proxy\" and \"self_rotate\" are accepted.")]
        string[] scopes,
        [Description(
            "Administrators only: the numeric id of the user to create the token for. Omit to create a token for the caller.")]
        long? userId = null,
        [Description("Optional free-text note describing the token's purpose.")]
        string? description = null,
        [Description("Optional expiry date, ISO 8601 (YYYY-MM-DD). Omit for GitLab's default.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Length == 0) throw new McpException("scopes must contain at least one scope.");

        var parsedExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt));

        GitLabPersonalAccessTokenWithSecret token;
        if (userId is { } id)
        {
            var request = new CreatePersonalAccessTokenRequest
                { Name = name, Scopes = scopes, Description = description, ExpiresAt = parsedExpiresAt };
            token = await personalAccessTokens.CreateForUserAsync(id, request, cancellationToken);
        }
        else
        {
            var request = new CreateCurrentUserPersonalAccessTokenRequest
                { Name = name, Scopes = scopes, Description = description, ExpiresAt = parsedExpiresAt };
            token = await personalAccessTokens.CreateForCurrentUserAsync(request, cancellationToken);
        }

        return GitLabContent.Wrap(
            PeopleMapper.ToCreated(token),
            userId is null ? "user/personal_access_tokens (create)" : "users/:id/personal_access_tokens (create)");
    }

    [McpServerTool(Name = "gitlab_manage_group_provider_identity", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Re-points a group's SAML or SCIM identity at a new external UID, or deletes it to unlink the user from the provider (for SCIM, deletion can also block the linked user, depending on the group's settings). Premium/Ultimate feature on a top-level group.")]
    public async Task<CallToolResult> ManageGroupProviderIdentityAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("Which identity kind to change: \"saml\" or \"scim\".")]
        string kind,
        [Description("The operation: \"update\" or \"delete\".")]
        string action,
        [Description("The identity's current external UID.")]
        string externUid,
        [Description("Required when action is \"update\": the new external UID to point the identity at.")]
        string? newExternUid = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = kind.ToLowerInvariant();
        if (normalizedKind is not ("saml" or "scim")) throw new McpException("kind must be \"saml\" or \"scim\".");

        var normalizedAction = action.ToLowerInvariant();

        switch (normalizedAction)
        {
            case "update":
            {
                if (string.IsNullOrWhiteSpace(newExternUid))
                    throw new McpException("newExternUid is required when action is \"update\".");

                var request = new UpdateProviderIdentityRequest { ExternUid = newExternUid };
                var identity = normalizedKind == "saml"
                    ? await providerIdentities.UpdateSamlAsync(group, externUid, request, cancellationToken)
                    : await providerIdentities.UpdateScimAsync(group, externUid, request, cancellationToken);

                var source = normalizedKind == "saml"
                    ? "groups/:id/saml/:uid (update)"
                    : "groups/:id/scim/:uid (update)";
                return GitLabContent.Wrap(
                    new ProviderIdentityManageResult(normalizedKind, normalizedAction,
                        PeopleMapper.ToSummary(identity)), source);
            }

            case "delete":
            {
                if (normalizedKind == "saml")
                    await providerIdentities.DeleteSamlAsync(group, externUid, cancellationToken);
                else
                    await providerIdentities.DeleteScimAsync(group, externUid, cancellationToken);

                var source = normalizedKind == "saml"
                    ? "groups/:id/saml/:uid (delete)"
                    : "groups/:id/scim/:uid (delete)";
                return GitLabContent.Wrap(new ProviderIdentityManageResult(normalizedKind, normalizedAction, null),
                    source);
            }

            default:
                throw new McpException("action must be \"update\" or \"delete\".");
        }
    }

    // ---- backlog-split/people/part-3.json below ----

    [McpServerTool(Name = "gitlab_rotate_personal_access_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Rotates a personal access token: the old token is revoked immediately and a new plaintext secret is returned once, and never shown again. Rotates the token identified by tokenId, or, when tokenId is omitted, the token that authenticated this call (which then requires the \"self_rotate\" scope).")]
    public async Task<CallToolResult> RotatePersonalAccessTokenAsync(
        [Description("The numeric id of the token to rotate. Omit to rotate the token that authenticated this call.")]
        long? tokenId = null,
        [Description(
            "Optional expiry date for the replacement token, ISO 8601 (YYYY-MM-DD). Omit for GitLab's default.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var request = new RotateAccessTokenRequest { ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt)) };

        var token = tokenId is { } id
            ? await personalAccessTokens.RotateAsync(id, request, cancellationToken)
            : await personalAccessTokens.RotateSelfAsync(request, cancellationToken);

        return GitLabContent.Wrap(PeopleMapper.ToCreated(token),
            tokenId is null ? "personal_access_tokens/self/rotate" : "personal_access_tokens/:id/rotate");
    }

    [McpServerTool(Name = "gitlab_revoke_personal_access_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Revokes a personal access token by tokenId, or, when tokenId is omitted, the token that authenticated this call -- every subsequent call from this client then fails.")]
    public async Task<PersonalAccessTokenRevocationResult> RevokePersonalAccessTokenAsync(
        [Description("The numeric id of the token to revoke. Omit to revoke the token that authenticated this call.")]
        long? tokenId = null,
        CancellationToken cancellationToken = default)
    {
        if (tokenId is { } id)
            await personalAccessTokens.RevokeAsync(id, cancellationToken);
        else
            await personalAccessTokens.RevokeSelfAsync(cancellationToken);

        return new PersonalAccessTokenRevocationResult(tokenId);
    }

    [McpServerTool(Name = "gitlab_revoke_impersonation_token", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description("Revokes a user's impersonation token. Administrators only.")]
    public async Task<ImpersonationTokenRevocationResult> RevokeImpersonationTokenAsync(
        [Description("The numeric id of the user the impersonation token belongs to.")]
        long userId,
        [Description("The numeric id of the impersonation token to revoke.")]
        long tokenId,
        CancellationToken cancellationToken = default)
    {
        await personalAccessTokens.RevokeImpersonationTokenAsync(userId, tokenId, cancellationToken);
        return new ImpersonationTokenRevocationResult(userId, tokenId);
    }

    [McpServerTool(Name = "gitlab_get_resource_access_token", ReadOnly = true, OpenWorld = false)]
    [Description("Gets one project or group access token by id. Never returns the plaintext secret.")]
    public async Task<CallToolResult> GetResourceAccessTokenAsync(
        [Description("The numeric id of the token to look up.")]
        long tokenId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        var token = scope == "project"
            ? await accessTokens.GetForProjectAsync(project!, tokenId, cancellationToken)
            : await accessTokens.GetForGroupAsync(group!, tokenId, cancellationToken);

        var source = scope == "project" ? "projects/:id/access_tokens/:token_id" : "groups/:id/access_tokens/:token_id";
        return GitLabContent.Wrap(PeopleMapper.ToSummary(token), source);
    }

    [McpServerTool(Name = "gitlab_rotate_resource_access_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Rotates a project or group access token: the old token is revoked immediately and a new plaintext secret is returned once, and never shown again. Rotates the token identified by tokenId, or, when tokenId is omitted, the token that authenticated this call (which then requires the \"self_rotate\" scope).")]
    public async Task<CallToolResult> RotateResourceAccessTokenAsync(
        [Description("The numeric id of the token to rotate. Omit to rotate the token that authenticated this call.")]
        long? tokenId = null,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description(
            "Optional expiry date for the replacement token, ISO 8601 (YYYY-MM-DD). Omit for GitLab's default.")]
        string? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);
        var request = new RotateAccessTokenRequest { ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt)) };

        var token = scope == "project"
            ? tokenId is { } projectTokenId
                ? await accessTokens.RotateForProjectAsync(project!, projectTokenId, request, cancellationToken)
                : await accessTokens.RotateSelfForProjectAsync(project!, request, cancellationToken)
            : tokenId is { } groupTokenId
                ? await accessTokens.RotateForGroupAsync(group!, groupTokenId, request, cancellationToken)
                : await accessTokens.RotateSelfForGroupAsync(group!, request, cancellationToken);

        var source = scope == "project"
            ? tokenId is null ? "projects/:id/access_tokens/self/rotate" : "projects/:id/access_tokens/:token_id/rotate"
            : tokenId is null
                ? "groups/:id/access_tokens/self/rotate"
                : "groups/:id/access_tokens/:token_id/rotate";
        return GitLabContent.Wrap(PeopleMapper.ToCreated(token), source);
    }

    [McpServerTool(Name = "gitlab_revoke_resource_access_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Revokes a project or group access token by id.")]
    public async Task<ResourceAccessTokenRevocationResult> RevokeResourceAccessTokenAsync(
        [Description("The numeric id of the token to revoke.")]
        long tokenId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (scope == "project")
            await accessTokens.RevokeForProjectAsync(project!, tokenId, cancellationToken);
        else
            await accessTokens.RevokeForGroupAsync(group!, tokenId, cancellationToken);

        return new ResourceAccessTokenRevocationResult(tokenId, scope);
    }

    [McpServerTool(Name = "gitlab_list_service_account_access_tokens", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists the personal access tokens belonging to a project's or group's service account. Never returns a plaintext secret.")]
    public async Task<CallToolResult> ListServiceAccountAccessTokensAsync(
        [Description("The numeric user id of the service account (not the caller's own id).")]
        long userId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description("Optional state filter: \"active\" or \"inactive\". Omit for all.")]
        string? state = null,
        [Description("Maximum tokens to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var options = new AccessTokenListOptions { State = state, PerPage = Math.Min(limit + 1, MaxLimit) };

        var stream = scope == "project"
            ? personalAccessTokens.ListForProjectServiceAccountAsync(project!, userId, options, cancellationToken)
            : personalAccessTokens.ListForGroupServiceAccountAsync(group!, userId, options, cancellationToken);

        List<PersonalAccessTokenSummary> collected = [];
        var truncated = false;

        await foreach (var token in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToResult(token));
        }

        var source = scope == "project"
            ? "projects/:id/service_accounts/:user_id/personal_access_tokens"
            : "groups/:id/service_accounts/:user_id/personal_access_tokens";
        return GitLabContent.Wrap(new PersonalAccessTokenListResult(collected, truncated), source);
    }

    [McpServerTool(Name = "gitlab_manage_service_account_access_token", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description(
        "Creates, rotates, or revokes a personal access token for a project's or group's service account. Create and rotate return a plaintext secret exactly once, and never show it again; revoke returns no secret.")]
    public async Task<CallToolResult> ManageServiceAccountAccessTokenAsync(
        [Description("The operation: \"create\", \"rotate\", or \"revoke\".")]
        string action,
        [Description("The numeric user id of the service account (not the caller's own id).")]
        long userId,
        [Description(
            "Project: either the numeric project id or the URL-encoded \"namespace/path\" form. Provide this or group, not both.")]
        string? project = null,
        [Description("Group: either the numeric group id or its full path. Provide this or project, not both.")]
        string? group = null,
        [Description("Required for \"rotate\" and \"revoke\": the numeric id of the existing token.")]
        long? tokenId = null,
        [Description("Required for \"create\": a display name for the token.")]
        string? name = null,
        [Description("Required for \"create\": at least one GitLab API scope, e.g. \"api\", \"read_api\".")]
        string[]? scopes = null,
        [Description("Create/rotate only: optional expiry date, ISO 8601 (YYYY-MM-DD). Omit for GitLab's default.")]
        string? expiresAt = null,
        [Description("Create only: optional free-text note describing the token's purpose.")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);
        var normalizedAction = action.ToLowerInvariant();

        switch (normalizedAction)
        {
            case "create":
            {
                if (string.IsNullOrWhiteSpace(name) || scopes is null || scopes.Length == 0)
                    throw new McpException("name and scopes are required when action is \"create\".");

                var request = new CreatePersonalAccessTokenRequest
                {
                    Name = name,
                    Scopes = scopes,
                    Description = description,
                    ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt))
                };

                var created = scope == "project"
                    ? await personalAccessTokens.CreateForProjectServiceAccountAsync(project!, userId, request,
                        cancellationToken)
                    : await personalAccessTokens.CreateForGroupServiceAccountAsync(group!, userId, request,
                        cancellationToken);

                var createdSource = scope == "project"
                    ? "projects/:id/service_accounts/:user_id/personal_access_tokens (create)"
                    : "groups/:id/service_accounts/:user_id/personal_access_tokens (create)";
                return GitLabContent.Wrap(PeopleMapper.ToCreated(created), createdSource);
            }

            case "rotate":
            {
                if (tokenId is null) throw new McpException("tokenId is required when action is \"rotate\".");

                var request = new RotateAccessTokenRequest { ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt)) };

                var rotated = scope == "project"
                    ? await personalAccessTokens.RotateForProjectServiceAccountAsync(project!, userId, tokenId.Value,
                        request, cancellationToken)
                    : await personalAccessTokens.RotateForGroupServiceAccountAsync(group!, userId, tokenId.Value,
                        request, cancellationToken);

                var rotatedSource = scope == "project"
                    ? "projects/:id/service_accounts/:user_id/personal_access_tokens/:token_id/rotate"
                    : "groups/:id/service_accounts/:user_id/personal_access_tokens/:token_id/rotate";
                return GitLabContent.Wrap(PeopleMapper.ToCreated(rotated), rotatedSource);
            }

            case "revoke":
            {
                if (tokenId is null) throw new McpException("tokenId is required when action is \"revoke\".");

                if (scope == "project")
                    await personalAccessTokens.RevokeForProjectServiceAccountAsync(project!, userId, tokenId.Value,
                        cancellationToken);
                else
                    await personalAccessTokens.RevokeForGroupServiceAccountAsync(group!, userId, tokenId.Value,
                        cancellationToken);

                var revokedSource = scope == "project"
                    ? "projects/:id/service_accounts/:user_id/personal_access_tokens/:token_id (revoke)"
                    : "groups/:id/service_accounts/:user_id/personal_access_tokens/:token_id (revoke)";
                return GitLabContent.Wrap(new ServiceAccountAccessTokenRevocationResult(userId, tokenId.Value),
                    revokedSource);
            }

            default:
                throw new McpException("action must be \"create\", \"rotate\", or \"revoke\".");
        }
    }

    [McpServerTool(Name = "gitlab_list_group_credential_inventory", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Lists personal access tokens, resource (project/group) access tokens, or SSH keys belonging to a top-level group's enterprise users, for a security audit across the whole group hierarchy. Requires the group to be on a top-level namespace with a paid plan and the caller to be a group Owner or instance administrator.")]
    public async Task<CallToolResult> ListGroupCredentialInventoryAsync(
        [Description("The top-level group's numeric id or full path.")]
        string group,
        [Description(
            "Which credential kind to list: \"personal_access_token\", \"resource_access_token\", or \"ssh_key\".")]
        string kind,
        [Description("Optional free-text search over the credential's name. Ignored for \"ssh_key\".")]
        string? search = null,
        [Description("Maximum entries to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = kind.ToLowerInvariant();
        if (normalizedKind is not ("personal_access_token" or "resource_access_token" or "ssh_key"))
            throw new McpException(
                "kind must be \"personal_access_token\", \"resource_access_token\", or \"ssh_key\".");

        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        switch (normalizedKind)
        {
            case "personal_access_token":
            {
                var options = new PersonalAccessTokenListOptions
                    { Search = search, PerPage = Math.Min(limit + 1, MaxLimit) };
                List<PersonalAccessTokenSummary> collected = [];
                var truncated = false;

                await foreach (var token in groupCredentialsInventory.ListPersonalAccessTokensAsync(group, options,
                                   cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(PeopleMapper.ToResult(token));
                }

                return GitLabContent.Wrap(new PersonalAccessTokenListResult(collected, truncated),
                    "groups/:id/manage/personal_access_tokens");
            }

            case "resource_access_token":
            {
                var options = new AccessTokenListOptions { Search = search, PerPage = Math.Min(limit + 1, MaxLimit) };
                List<ResourceAccessTokenSummary> collected = [];
                var truncated = false;

                await foreach (var token in groupCredentialsInventory.ListResourceAccessTokensAsync(group, options,
                                   cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(PeopleMapper.ToSummary(token));
                }

                return GitLabContent.Wrap(new ResourceAccessTokenListResult(collected, truncated),
                    "groups/:id/manage/resource_access_tokens");
            }

            default:
            {
                var options = new GroupManagedSshKeyListOptions { PerPage = Math.Min(limit + 1, MaxLimit) };
                List<GroupManagedSshKeySummary> collected = [];
                var truncated = false;

                await foreach (var key in groupCredentialsInventory.ListSshKeysAsync(group, options, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(PeopleMapper.ToSummary(key));
                }

                return GitLabContent.Wrap(new GroupManagedSshKeyListResult(collected, truncated),
                    "groups/:id/manage/ssh_keys");
            }
        }
    }

    [McpServerTool(Name = "gitlab_manage_group_credential", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Revokes or rotates a personal access token or resource (project/group) access token, or deletes an SSH key, belonging to one of a top-level group's enterprise users. Requires the group to be on a top-level namespace with a paid plan and the caller to be a group Owner or instance administrator. Rotating a personal access token returns a freshly disclosed plaintext secret, never shown again; rotating a resource access token does not.")]
    public async Task<CallToolResult> ManageGroupCredentialAsync(
        [Description("The top-level group's numeric id or full path.")]
        string group,
        [Description(
            "Which credential kind to change: \"personal_access_token\", \"resource_access_token\", or \"ssh_key\".")]
        string kind,
        [Description(
            "The operation: \"revoke\" or \"rotate\" for the two token kinds; \"delete\" for \"ssh_key\" (its only supported operation).")]
        string action,
        [Description("The numeric id of the token or SSH key to change.")]
        long credentialId,
        [Description(
            "Rotate only: optional expiry date for the replacement token, ISO 8601 (YYYY-MM-DD). Omit for GitLab's default.")]
        string? expiresAt = null,
        [Description(
            "resource_access_token revoke only: optional future date to schedule the revocation instead of taking effect immediately, ISO 8601 (YYYY-MM-DD). Omit to revoke immediately.")]
        string? scheduleRevokeAt = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = kind.ToLowerInvariant();
        var normalizedAction = action.ToLowerInvariant();

        switch (normalizedKind)
        {
            case "personal_access_token":
                switch (normalizedAction)
                {
                    case "revoke":
                        await groupCredentialsInventory.RevokePersonalAccessTokenAsync(group, credentialId,
                            cancellationToken);
                        return GitLabContent.Wrap(
                            new GroupCredentialManageResult(normalizedKind, normalizedAction, credentialId, null),
                            "groups/:id/manage/personal_access_tokens/:token_id (revoke)");

                    case "rotate":
                    {
                        var request = new RotateAccessTokenRequest
                            { ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt)) };
                        var rotated =
                            await groupCredentialsInventory.RotatePersonalAccessTokenAsync(group, credentialId, request,
                                cancellationToken);
                        return GitLabContent.Wrap(
                            new GroupCredentialManageResult(normalizedKind, normalizedAction, credentialId,
                                PeopleMapper.ToCreated(rotated).Token),
                            "groups/:id/manage/personal_access_tokens/:token_id/rotate");
                    }

                    default:
                        throw new McpException(
                            "action must be \"revoke\" or \"rotate\" for kind \"personal_access_token\".");
                }

            case "resource_access_token":
                switch (normalizedAction)
                {
                    case "revoke":
                        await groupCredentialsInventory.RevokeResourceAccessTokenAsync(
                            group, credentialId, ParseDateOnly(scheduleRevokeAt, nameof(scheduleRevokeAt)),
                            cancellationToken);
                        return GitLabContent.Wrap(
                            new GroupCredentialManageResult(normalizedKind, normalizedAction, credentialId, null),
                            "groups/:id/manage/resource_access_tokens/:token_id (revoke)");

                    case "rotate":
                    {
                        var request = new RotateAccessTokenRequest
                            { ExpiresAt = ParseDateOnly(expiresAt, nameof(expiresAt)) };
                        await groupCredentialsInventory.RotateResourceAccessTokenAsync(group, credentialId, request,
                            cancellationToken);
                        return GitLabContent.Wrap(
                            new GroupCredentialManageResult(normalizedKind, normalizedAction, credentialId, null),
                            "groups/:id/manage/resource_access_tokens/:token_id/rotate");
                    }

                    default:
                        throw new McpException(
                            "action must be \"revoke\" or \"rotate\" for kind \"resource_access_token\".");
                }

            case "ssh_key":
                if (normalizedAction != "delete")
                    throw new McpException("action must be \"delete\" for kind \"ssh_key\".");

                await groupCredentialsInventory.DeleteSshKeyAsync(group, credentialId, cancellationToken);
                return GitLabContent.Wrap(
                    new GroupCredentialManageResult(normalizedKind, normalizedAction, credentialId, null),
                    "groups/:id/manage/ssh_keys/:key_id (delete)");

            default:
                throw new McpException(
                    "kind must be \"personal_access_token\", \"resource_access_token\", or \"ssh_key\".");
        }
    }

    [McpServerTool(Name = "gitlab_get_ssh_key", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one SSH key by id: the caller's own (default), another user's via userId, or, as an administrator, by the key's own instance-wide id via byInstanceId -- which is the only form that also returns the key's owner.")]
    public async Task<CallToolResult> GetSshKeyAsync(
        [Description("The numeric id of the SSH key.")]
        long keyId,
        [Description(
            "The numeric id of another user whose key to look up. Omit to look up the caller's own key. Ignored when byInstanceId is true.")]
        long? userId = null,
        [Description(
            "Administrators only: when true, keyId is treated as an instance-wide key id rather than one scoped to a user, and the result includes the key's owner. Default false.")]
        bool byInstanceId = false,
        CancellationToken cancellationToken = default)
    {
        GitLabSshKey key;
        string source;

        if (byInstanceId)
        {
            key = await sshKeys.GetByIdAsync(keyId, cancellationToken);
            source = "keys/:id";
        }
        else if (userId is { } id)
        {
            key = await sshKeys.GetForUserAsync(id, keyId, cancellationToken);
            source = "users/:user_id/keys/:key_id";
        }
        else
        {
            key = await sshKeys.GetForCurrentUserAsync(keyId, cancellationToken);
            source = "user/keys/:key_id";
        }

        return GitLabContent.Wrap(PeopleMapper.ToSummary(key), source);
    }

    [McpServerTool(Name = "gitlab_add_ssh_key", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds an SSH public key to the caller's account, or, as an administrator, to another user's account via userId.")]
    public async Task<CallToolResult> AddSshKeyAsync(
        [Description("A title for the key.")] string title,
        [Description("The SSH public key material, e.g. \"ssh-ed25519 AAAA... comment\".")]
        string key,
        [Description(
            "Administrators only: the numeric id of the user to add the key to. Omit to add to the caller's own account.")]
        long? userId = null,
        [Description("Optional expiry, ISO 8601 date-time. Omit for no expiry.")]
        string? expiresAt = null,
        [Description("Optional usage restriction: \"auth\" or \"signing\". Omit for GitLab's default (both).")]
        string? usageType = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateSshKeyRequest
        {
            Title = title,
            Key = key,
            ExpiresAt = ParseDateTimeOffset(expiresAt, nameof(expiresAt)),
            UsageType = usageType
        };

        var created = userId is { } id
            ? await sshKeys.CreateForUserAsync(id, request, cancellationToken)
            : await sshKeys.CreateForCurrentUserAsync(request, cancellationToken);

        return GitLabContent.Wrap(PeopleMapper.ToSummary(created),
            userId is null ? "user/keys (add)" : "users/:id/keys (add)");
    }

    [McpServerTool(Name = "gitlab_delete_ssh_key", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Removes an SSH key from the caller's account, or, as an administrator, from another user's account via userId.")]
    public async Task<SshKeyDeletionResult> DeleteSshKeyAsync(
        [Description("The numeric id of the SSH key to remove.")]
        long keyId,
        [Description(
            "Administrators only: the numeric id of the user to remove the key from. Omit to remove from the caller's own account.")]
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId is { } id)
            await sshKeys.DeleteForUserAsync(id, keyId, cancellationToken);
        else
            await sshKeys.DeleteForCurrentUserAsync(keyId, cancellationToken);

        return new SshKeyDeletionResult(keyId, userId);
    }

    [McpServerTool(Name = "gitlab_find_ssh_key_owner", ReadOnly = true, OpenWorld = false)]
    [Description("Resolves an SSH key fingerprint (MD5 or SHA-256) to the user that owns it. Administrators only.")]
    public async Task<CallToolResult> FindSshKeyOwnerAsync(
        [Description("The key fingerprint, either MD5 (\"9a:2b:...\") or SHA-256 (\"SHA256:base64...\").")]
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var user = await sshKeys.GetUserByFingerprintAsync(fingerprint, cancellationToken);
        return GitLabContent.Wrap(PeopleMapper.ToSummary(user), "keys (fingerprint lookup)");
    }

    [McpServerTool(Name = "gitlab_manage_group_ssh_certificates", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false)]
    [Description("Lists, registers, or removes a group's trusted SSH certificate authorities.")]
    public async Task<CallToolResult> ManageGroupSshCertificatesAsync(
        [Description("The group's numeric id or full path.")]
        string group,
        [Description("The operation: \"list\", \"add\", or \"delete\".")]
        string action,
        [Description("Required for \"add\": a title for the certificate authority.")]
        string? title = null,
        [Description("Required for \"add\": the certificate authority's public key material.")]
        string? key = null,
        [Description("Required for \"delete\": the numeric id of the certificate authority to remove.")]
        long? certificateId = null,
        [Description("Maximum certificates to return for \"list\" (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.ToLowerInvariant();

        switch (normalizedAction)
        {
            case "list":
            {
                if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

                List<SshCertificateSummary> collected = [];
                var truncated = false;

                await foreach (var certificate in sshKeys.ListGroupCertificatesAsync(group, cancellationToken))
                {
                    if (collected.Count == limit)
                    {
                        truncated = true;
                        break;
                    }

                    collected.Add(PeopleMapper.ToSummary(certificate));
                }

                return GitLabContent.Wrap(new SshCertificateListResult(collected, truncated),
                    "groups/:id/ssh_certificates");
            }

            case "add":
            {
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(key))
                    throw new McpException("title and key are required when action is \"add\".");

                var request = new CreateSshCertificateRequest { Title = title, Key = key };
                var created = await sshKeys.AddGroupCertificateAsync(group, request, cancellationToken);
                return GitLabContent.Wrap(new SshCertificateListResult([PeopleMapper.ToSummary(created)], false),
                    "groups/:id/ssh_certificates (add)");
            }

            case "delete":
            {
                if (certificateId is null)
                    throw new McpException("certificateId is required when action is \"delete\".");

                await sshKeys.DeleteGroupCertificateAsync(group, certificateId.Value, cancellationToken);
                return GitLabContent.Wrap(new SshCertificateListResult([], false),
                    "groups/:id/ssh_certificates/:id (delete)");
            }

            default:
                throw new McpException("action must be \"list\", \"add\", or \"delete\".");
        }
    }

    [McpServerTool(Name = "gitlab_list_gpg_keys", ReadOnly = true, OpenWorld = false)]
    [Description("Lists the caller's GPG keys, or another user's via userId.")]
    public async Task<CallToolResult> ListGpgKeysAsync(
        [Description("The numeric id of another user whose GPG keys to list. Omit to list the caller's own.")]
        long? userId = null,
        [Description("Maximum keys to return (1-100). Default 20.")]
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxLimit) throw new McpException($"limit must be between 1 and {MaxLimit}.");

        var stream = userId is { } id
            ? gpgKeys.ListForUserAsync(id, cancellationToken)
            : gpgKeys.ListForCurrentUserAsync(cancellationToken);

        List<GpgKeySummary> collected = [];
        var truncated = false;

        await foreach (var key in stream)
        {
            if (collected.Count == limit)
            {
                truncated = true;
                break;
            }

            collected.Add(PeopleMapper.ToSummary(key));
        }

        return GitLabContent.Wrap(new GpgKeyListResult(collected, truncated),
            userId is null ? "user/gpg_keys" : "users/:id/gpg_keys");
    }

    [McpServerTool(Name = "gitlab_add_gpg_key", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Adds a GPG public key to the caller's account, or, as an administrator, to another user's account via userId.")]
    public async Task<CallToolResult> AddGpgKeyAsync(
        [Description("The armored GPG public key block.")]
        string key,
        [Description(
            "Administrators only: the numeric id of the user to add the key to. Omit to add to the caller's own account.")]
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateGpgKeyRequest { Key = key };

        var created = userId is { } id
            ? await gpgKeys.CreateForUserAsync(id, request, cancellationToken)
            : await gpgKeys.CreateForCurrentUserAsync(request, cancellationToken);

        return GitLabContent.Wrap(PeopleMapper.ToSummary(created),
            userId is null ? "user/gpg_keys (add)" : "users/:id/gpg_keys (add)");
    }

    [McpServerTool(Name = "gitlab_manage_gpg_key", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes or revokes a GPG key from the caller's account, or, as an administrator, from another user's account via userId. Revoking also marks every commit signed with that key as unverified and is stronger than deleting; deleting does not touch signature verification.")]
    public async Task<GpgKeyManageResult> ManageGpgKeyAsync(
        [Description("The numeric id of the GPG key to change.")]
        long keyId,
        [Description("The operation: \"delete\" or \"revoke\".")]
        string action,
        [Description(
            "Administrators only: the numeric id of the user the key belongs to. Omit for the caller's own key.")]
        long? userId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.ToLowerInvariant();

        var task = (normalizedAction, userId) switch
        {
            ("delete", { } id) => gpgKeys.DeleteForUserAsync(id, keyId, cancellationToken),
            ("delete", null) => gpgKeys.DeleteForCurrentUserAsync(keyId, cancellationToken),
            ("revoke", { } id) => gpgKeys.RevokeForUserAsync(id, keyId, cancellationToken),
            ("revoke", null) => gpgKeys.RevokeForCurrentUserAsync(keyId, cancellationToken),
            _ => throw new McpException("action must be \"delete\" or \"revoke\".")
        };

        await task;
        return new GpgKeyManageResult(keyId, normalizedAction, userId);
    }

    // ---- backlog-split/people/part-4.json below ----

    [McpServerTool(Name = "gitlab_get_service_account", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Gets one service account owned by a group or project, by its numeric user id. Requires group Owner access for a group-owned account, or project Owner/Maintainer access for a project-owned account. Instance-owned accounts have no get-one route in the GitLab API; look one up with gitlab_get_user instead. Service accounts are a Premium/Ultimate feature; a Free-tier instance answers 403.")]
    public async Task<CallToolResult> GetServiceAccountAsync(
        [Description("The service account's numeric user id.")]
        long userId,
        [Description(
            "The account's project, if it is project-owned: numeric id or URL-encoded \"namespace/path\". Provide exactly one of project or group.")]
        string? project = null,
        [Description(
            "The account's group, if it is group-owned: numeric id or full path. Provide exactly one of project or group.")]
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        var account = scope == "project"
            ? await serviceAccounts.GetForProjectAsync(project!, userId, cancellationToken)
            : await serviceAccounts.GetForGroupAsync(group!, userId, cancellationToken);

        return GitLabContent.Wrap(PeopleMapper.ToSummary(account),
            scope == "project" ? "projects/:id/service_accounts/:user_id" : "groups/:id/service_accounts/:user_id");
    }

    [McpServerTool(Name = "gitlab_manage_service_account", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Creates a service account at instance, group, or project scope, or updates an existing service account's display name, username, or email. Every field left unset is auto-generated by GitLab on create, or left unchanged on update; a changed email does not take effect until the new address is confirmed. Creating or updating an instance-scoped account requires instance administrator rights; a group-scoped account requires group Owner access; a project-scoped account requires project Owner or Maintainer access. Service accounts are a Premium/Ultimate feature; a Free-tier instance answers 403.")]
    public async Task<CallToolResult> ManageServiceAccountAsync(
        [Description("The operation: \"create\" or \"update\".")]
        string action,
        [Description("Where the service account is owned: \"instance\", \"group\", or \"project\".")]
        string scope,
        [Description(
            "Required when scope is \"project\": the numeric project id or the URL-encoded \"namespace/path\" form.")]
        string? project = null,
        [Description("Required when scope is \"group\": the numeric group id or its full path.")]
        string? group = null,
        [Description(
            "Required when action is \"update\": the numeric user id of the service account to change. Ignored for \"create\", where GitLab assigns the id.")]
        long? userId = null,
        [Description(
            "The account's display name. Omit on create to let GitLab generate one; omit on update to leave it unchanged.")]
        string? name = null,
        [Description(
            "The account's username. Omit on create to let GitLab generate one; omit on update to leave it unchanged.")]
        string? username = null,
        [Description(
            "The account's email address. Omit on create to let GitLab generate a no-reply address; omit on update to leave it unchanged.")]
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = RequireServiceAccountScope(scope, project, group);
        var normalizedAction = action.ToLowerInvariant();

        GitLabServiceAccount account;

        switch (normalizedAction)
        {
            case "create":
            {
                var request = new CreateServiceAccountRequest { Name = name, Username = username, Email = email };

                account = normalizedScope switch
                {
                    "project" => await serviceAccounts.CreateForProjectAsync(project!, request, cancellationToken),
                    "group" => await serviceAccounts.CreateForGroupAsync(group!, request, cancellationToken),
                    _ => await serviceAccounts.CreateAsync(request, cancellationToken)
                };
                break;
            }

            case "update":
            {
                if (userId is not { } id) throw new McpException("userId is required when action is \"update\".");

                var request = new UpdateServiceAccountRequest { Name = name, Username = username, Email = email };

                account = normalizedScope switch
                {
                    "project" => await serviceAccounts.UpdateForProjectAsync(project!, id, request, cancellationToken),
                    "group" => await serviceAccounts.UpdateForGroupAsync(group!, id, request, cancellationToken),
                    _ => await serviceAccounts.UpdateAsync(id, request, cancellationToken)
                };
                break;
            }

            default:
                throw new McpException("action must be \"create\" or \"update\".");
        }

        var source = normalizedScope switch
        {
            "project" => "projects/:id/service_accounts",
            "group" => "groups/:id/service_accounts",
            _ => "service_accounts"
        };

        return GitLabContent.Wrap(PeopleMapper.ToSummary(account), source);
    }

    [McpServerTool(Name = "gitlab_delete_service_account", ReadOnly = false, Destructive = true, Idempotent = false,
        OpenWorld = false)]
    [Description(
        "Deletes a group- or project-owned service account. Requires administrator access. Set hardDelete to permanently remove the account's contributions (commits, issues, comments, ...) instead of reassigning them to GitLab's Ghost User placeholder — this cannot be undone. Instance-owned accounts have no delete route in the GitLab API.")]
    public async Task<ServiceAccountDeletionResult> DeleteServiceAccountAsync(
        [Description("The service account's numeric user id.")]
        long userId,
        [Description(
            "The account's project, if it is project-owned: numeric id or URL-encoded \"namespace/path\". Provide exactly one of project or group.")]
        string? project = null,
        [Description(
            "The account's group, if it is group-owned: numeric id or full path. Provide exactly one of project or group.")]
        string? group = null,
        [Description(
            "When true, permanently deletes the account's contributions instead of moving them to the Ghost User. Irreversible. Default false.")]
        bool? hardDelete = null,
        CancellationToken cancellationToken = default)
    {
        var scope = RequireOneScope(project, group);

        if (scope == "project")
            await serviceAccounts.DeleteForProjectAsync(project!, userId, hardDelete, cancellationToken);
        else
            await serviceAccounts.DeleteForGroupAsync(group!, userId, hardDelete, cancellationToken);

        return new ServiceAccountDeletionResult(userId, scope, hardDelete ?? false);
    }
}