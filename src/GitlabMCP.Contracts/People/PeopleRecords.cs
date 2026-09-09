namespace GitlabMCP.Contracts.People;

/// <summary>
///     Users, members, tokens and access projection records (People domain). Every record here was read
///     field-by-field against the mcp-untrusted-content Step 3 secret/PII inventory before being written:
///     <c>GitLabUser.Email</c>/<c>.CommitEmail</c>/<c>.Identities</c>/<c>.ScimIdentities</c>/<c>.IsAdmin</c>/
///     <c>.Note</c>/<c>.CustomAttributes</c> and anything named <c>*Token</c> are never projected, except the
///     two tools whose entire purpose is minting a credential and returning its plaintext exactly once
///     (<see cref="ImpersonationTokenCreated" />, <see cref="RunnerRegistrationResult" />,
///     <see cref="ResourceAccessTokenCreated" />) — each documented at its declaration.
/// </summary>
public sealed record EnterpriseUserSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    bool? Locked,
    string? WebUrl,
    DateTimeOffset? CreatedAt);

public sealed record EnterpriseUserListResult(IReadOnlyList<EnterpriseUserSummary> Users, bool Truncated);

/// <summary>
///     Never carries the plaintext secret — <c>GitLabImpersonationToken</c> (the list/get shape) has no
///     <c>Token</c> member at all; only <c>GitLabImpersonationTokenWithSecret</c> (create) does.
/// </summary>
public sealed record ImpersonationTokenSummary(
    long Id,
    string? Name,
    bool? Active,
    bool? Revoked,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record ImpersonationTokenListResult(IReadOnlyList<ImpersonationTokenSummary> Tokens, bool Truncated);

/// <summary>
///     The one deliberate exception to the no-secrets rule: minting an impersonation token has no purpose
///     unless the plaintext reaches the caller, and GitLab discloses it exactly once, on this response.
/// </summary>
public sealed record ImpersonationTokenCreated(
    long Id,
    string? Name,
    string Token,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record MemberSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    int AccessLevel,
    bool? Locked,
    bool? IsUsingSeat,
    DateTimeOffset? CreatedAt,
    DateOnly? ExpiresAt,
    string? WebUrl);

public sealed record MemberListResult(IReadOnlyList<MemberSummary> Members, bool Truncated);

/// <summary>
///     <see cref="Owner" /> is populated only when the key came from
///     <c>ISshKeysClient.GetByIdAsync</c> (the instance-wide admin lookup, which is the only endpoint that
///     returns the owner alongside the key) — every other endpoint leaves it null.
/// </summary>
public sealed record SshKeySummary(
    long Id,
    string? Title,
    string? Key,
    string? UsageType,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    EnterpriseUserSummary? Owner = null);

public sealed record SshKeyListResult(IReadOnlyList<SshKeySummary> Keys, bool Truncated);

public sealed record ResourceAccessTokenSummary(
    long Id,
    string? Name,
    string? Description,
    bool? Active,
    bool? Revoked,
    IReadOnlyList<string> Scopes,
    int? AccessLevel,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record ResourceAccessTokenListResult(IReadOnlyList<ResourceAccessTokenSummary> Tokens, bool Truncated);

/// <summary>
///     The other deliberate secret exception: creating a project/group access token is the only moment
///     GitLab discloses its plaintext.
/// </summary>
public sealed record ResourceAccessTokenCreated(
    long Id,
    string? Name,
    string Token,
    IReadOnlyList<string> Scopes,
    int? AccessLevel,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
///     <c>Email</c>/<c>UnconfirmedEmail</c> are deliberately omitted even though <c>GitLabServiceAccount</c>
///     is not the <c>GitLabUser</c> type the CLAUDE.md ban names explicitly — a bot account's mailbox is
///     still PII-shaped, so this record treats it the same way. <c>PublicEmail</c> is kept: it is the field
///     GitLab itself calls out as opted-in-public.
/// </summary>
public sealed record ServiceAccountSummary(long Id, string? Username, string? Name, string? PublicEmail);

public sealed record ServiceAccountListResult(IReadOnlyList<ServiceAccountSummary> Accounts, bool Truncated);

/// <summary>
///     <c>GitLabMemberRole</c> spreads its ~40 grantable abilities across one bool property per ability;
///     <see cref="EnabledAbilities" /> collapses them to the ones actually turned on, via
///     <c>PeopleMapper.EnabledAbilities</c>, instead of shipping ~40 mostly-false fields per role.
/// </summary>
public sealed record MemberRoleSummary(
    long Id,
    long? GroupId,
    string? Name,
    string? Description,
    int? BaseAccessLevel,
    IReadOnlyList<string> EnabledAbilities);

public sealed record MemberRoleListResult(IReadOnlyList<MemberRoleSummary> Roles, bool Truncated);

public sealed record AccessRequestSummary(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl,
    DateTimeOffset? RequestedAt);

public sealed record AccessRequestListResult(IReadOnlyList<AccessRequestSummary> Requests, bool Truncated);

/// <summary>
///     GitLab's state-move endpoint returns no body; every field here is either the caller's own validated
///     input or a literal the server produced — no GitLab-authored string, so this is the rare bare-record
///     (unwrapped) case (CLAUDE.md rule 4 / mcp-tool-authoring Step 4).
/// </summary>
public sealed record GroupMemberStateUpdateResult(long UserId, string NewState);

/// <summary>
///     The third deliberate secret exception: registering a runner has no purpose unless the one-time
///     registration token reaches the caller.
/// </summary>
public sealed record RunnerRegistrationResult(long Id, string Token, DateTimeOffset? TokenExpiresAt);

public sealed record EnterpriseUserUpdateResult(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl,
    int? ProjectsLimit,
    bool? CanCreateGroup);

/// <summary>
///     <c>GitLabInvitation.InviteToken</c> is never projected — it is a bearer credential for accepting the
///     invitation and matches the CLAUDE.md "anything named <c>*Token</c>" ban.
/// </summary>
public sealed record InvitationResult(
    int? AccessLevel,
    string? InviteEmail,
    string? UserName,
    string? CreatedByName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record InvitationListResult(IReadOnlyList<InvitationResult> Invitations, bool Truncated);

/// <summary>
///     Deleting an invitation returns no body; <see cref="Email" /> is the caller's own input, not
///     GitLab-authored text, so this is the rare bare-record case (mcp-tool-authoring Step 4).
/// </summary>
public sealed record InvitationDeletionResult(string Email);

/// <summary>
///     GitLab's pending-members endpoint documents no fixed response schema and every field is optional on
///     purpose: an unredeemed email invite carries only <see cref="Email" />, with no <see cref="Id" />,
///     <see cref="Username" /> or <see cref="WebUrl" /> — mirroring <c>GitLabPendingMember</c>. Projecting
///     <see cref="Email" /> here is deliberate, not an oversight of the mcp-untrusted-content PII rule: for an
///     unredeemed invite it is the *only* identifier GitLab returns, and the existing invitation tools in
///     this same file already expose <c>InviteEmail</c> for the identical reason.
/// </summary>
public sealed record PendingGroupMemberSummary(
    long? Id,
    string? Name,
    string? Username,
    string? Email,
    string? WebUrl,
    bool? Approved,
    bool? Invited);

public sealed record PendingGroupMemberListResult(IReadOnlyList<PendingGroupMemberSummary> Members, bool Truncated);

/// <summary>
///     Both the one-membership and approve-all endpoints return no body; every field here is the caller's
///     own input, so this is the rare bare-record case.
/// </summary>
public sealed record ApprovePendingGroupMembersResult(long? MembershipId, bool ApprovedAll);

/// <summary>
///     Removing a member returns no body; <see cref="Scope" /> is one of the two literals the tool itself
///     chose ("project" or "group"), not GitLab-authored text, so this is the rare bare-record case.
/// </summary>
public sealed record MemberRemovalResult(long UserId, string Scope);

/// <summary>
///     Deleting a member role returns no body; <see cref="Scope" /> is one of the tool's own literals
///     ("instance", "group", "admin"), not GitLab-authored text, so this is the rare bare-record case.
/// </summary>
public sealed record MemberRoleDeletionResult(long MemberRoleId, string Scope);

/// <summary>
///     Approving returns the new <see cref="GitLabMember" /> (projected via <see cref="Member" />); denying
///     returns no body at all. One record covers both so the tool declares a single return shape — GitLab
///     text can appear in <see cref="Member" />, so the whole record is wrapped either way.
/// </summary>
public sealed record AccessRequestReviewResult(long UserId, string Decision, MemberSummary? Member);

public sealed record SamlGroupLinkSummary(string? Name, int AccessLevel, long? MemberRoleId, string? Provider);

public sealed record SamlGroupLinkListResult(IReadOnlyList<SamlGroupLinkSummary> Links, bool Truncated);

/// <summary>
///     Deleting a SAML group link returns no body; both fields are the caller's own input, so this is the
///     rare bare-record case.
/// </summary>
public sealed record SamlGroupLinkDeletionResult(string SamlGroupName, string? Provider);

public sealed record ProviderIdentitySummary(string? ExternUid, long? UserId, bool? Active);

public sealed record ProviderIdentityListResult(IReadOnlyList<ProviderIdentitySummary> Identities, bool Truncated);

// ---- backlog-split/people/part-2.json below ----

/// <summary>
///     Covers both branches <c>gitlab_manage_group_provider_identity</c> can take: <see cref="Identity" /> is
///     populated for "update" (GitLab returns the re-pointed identity) and null for "delete" (no body).
/// </summary>
public sealed record ProviderIdentityManageResult(string Kind, string Action, ProviderIdentitySummary? Identity);

/// <summary>
///     <c>GitLabNotificationSettings</c> spreads 24 grantable event flags across one bool property each;
///     <see cref="EnabledEvents" /> collapses them to the ones actually turned on, via
///     <c>PeopleMapper.EnabledNotificationEvents</c>, the same pattern <see cref="MemberRoleSummary" /> uses
///     for member-role abilities.
/// </summary>
public sealed record NotificationSettingsResult(
    string? Level,
    string? NotificationEmail,
    IReadOnlyList<string> EnabledEvents);

public sealed record CurrentUserResult(EnterpriseUserSummary Account, UserCountsSummary? Counts);

public sealed record UserCountsSummary(
    int? MergeRequests,
    int? AssignedIssues,
    int? AssignedMergeRequests,
    int? ReviewRequestedMergeRequests,
    int? Todos);

public sealed record EmailSummary(long Id, string? Email, DateTimeOffset? ConfirmedAt);

/// <summary>
///     Covers all three branches of <c>gitlab_manage_current_user_email</c>: <see cref="Emails" />/
///     <see cref="Truncated" /> for "list", <see cref="Email" /> for "add", <see cref="DeletedEmailId" /> for
///     "delete" — the unused fields for whichever branch ran are left null, the same discriminated shape
///     <see cref="AccessRequestReviewResult" /> already uses.
/// </summary>
public sealed record CurrentUserEmailResult(
    string Action,
    IReadOnlyList<EmailSummary>? Emails,
    bool Truncated,
    EmailSummary? Email,
    long? DeletedEmailId);

/// <summary>
///     Every field is a bool preference flag GitLab returns as a plain member, never free text, so this is
///     the rare bare-record (unwrapped) case (CLAUDE.md rule 4 / mcp-tool-authoring Step 4).
/// </summary>
public sealed record UserPreferencesResult(
    bool? ViewDiffsFileByFile,
    bool? ShowWhitespaceInDiffs,
    bool? PassUserIdentitiesToCiJwt,
    bool? PolicyAdvancedEditor);

/// <summary>
///     Shared by <c>gitlab_manage_current_user_status</c> and <c>gitlab_get_user_status</c> — both read a
///     <c>GitLabUserStatus</c>-shaped payload.
/// </summary>
public sealed record UserStatusResult(
    string? Emoji,
    string? Message,
    string? Availability,
    DateTimeOffset? ClearStatusAt);

/// <summary>
///     A fourth deliberate secret exception alongside <see cref="ImpersonationTokenCreated" />,
///     <see cref="RunnerRegistrationResult" /> and <see cref="ResourceAccessTokenCreated" />: the whole purpose
///     of <c>gitlab_manage_current_user_support_pin</c> is handing this PIN to its own owner. GitLab's own
///     doc says to treat it as a secret even though it authenticates nothing by itself (GitLab Support asks
///     for it only to verify identity over a support channel).
/// </summary>
public sealed record SupportPinResult(string? Pin, DateTimeOffset? ExpiresAt);

public sealed record UserListResult(IReadOnlyList<EnterpriseUserSummary> Users, bool Truncated);

public sealed record UserManageResult(
    long Id,
    string? Username,
    string? Name,
    string? State,
    string? WebUrl,
    DateTimeOffset? CreatedAt);

/// <summary>
///     GitLab's delete-user endpoint returns no body; both fields are the caller's own input, so this is the
///     rare bare-record case.
/// </summary>
public sealed record UserDeletionResult(long UserId, bool HardDelete);

/// <summary>
///     Every one of the nine lifecycle endpoints this covers (activate/deactivate/block/unblock/ban/unban/
///     approve/reject/disable_two_factor) returns no body; both fields are the caller's own input, so this
///     is the rare bare-record case.
/// </summary>
public sealed record UserAccountStateResult(long UserId, string Action);

public sealed record PersonalAccessTokenSummary(
    long Id,
    string? Name,
    string? Description,
    bool? Active,
    bool? Revoked,
    IReadOnlyList<string> Scopes,
    long? UserId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record PersonalAccessTokenListResult(IReadOnlyList<PersonalAccessTokenSummary> Tokens, bool Truncated);

/// <summary>
///     The fifth deliberate secret exception: creating a personal access token is the only moment GitLab
///     discloses its plaintext.
/// </summary>
public sealed record PersonalAccessTokenCreated(
    long Id,
    string? Name,
    string Token,
    IReadOnlyList<string> Scopes,
    long? UserId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

// ---- backlog-split/people/part-3.json below ----

/// <summary>
///     GitLab's revoke endpoints return no body; <see cref="TokenId" /> is the caller's own input (null means
///     "the token that authenticated this call"), so this is the rare bare-record case.
/// </summary>
public sealed record PersonalAccessTokenRevocationResult(long? TokenId);

/// <summary>
///     GitLab's revoke-impersonation-token endpoint returns no body; both fields are the caller's own input,
///     so this is the rare bare-record case.
/// </summary>
public sealed record ImpersonationTokenRevocationResult(long UserId, long TokenId);

/// <summary>
///     GitLab's revoke endpoints return no body; every field here is the caller's own input, so this is the
///     rare bare-record case.
/// </summary>
public sealed record ResourceAccessTokenRevocationResult(long TokenId, string Scope);

public sealed record ServiceAccountAccessTokenRevocationResult(long UserId, long TokenId);

/// <summary>
///     Covers every branch <c>gitlab_manage_group_credential</c> can take. <see cref="RotatedSecret" /> is a
///     sixth deliberate secret exception alongside <see cref="ImpersonationTokenCreated" />,
///     <see cref="RunnerRegistrationResult" />, <see cref="ResourceAccessTokenCreated" /> and
///     <see cref="PersonalAccessTokenCreated" />: rotating a group-enterprise-user's personal access token is
///     the only moment GitLab discloses its plaintext, and it is populated only for that one branch — every
///     other branch (revoke, or rotating a resource access token, which GitLab answers with no content) leaves
///     it null.
/// </summary>
public sealed record GroupCredentialManageResult(string Kind, string Action, long CredentialId, string? RotatedSecret);

/// <summary>
///     <c>GitLabGroupManagedSshKey</c> (the shape <c>IGroupCredentialsInventoryClient.ListSshKeysAsync</c>
///     returns) has no <c>Key</c> member at all, unlike the ordinary <see cref="SshKeySummary" /> shape — the
///     group credentials inventory lists the key's metadata for an audit, not its public material.
/// </summary>
public sealed record GroupManagedSshKeySummary(
    long Id,
    string? Title,
    string? UsageType,
    long? UserId,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt);

public sealed record GroupManagedSshKeyListResult(IReadOnlyList<GroupManagedSshKeySummary> Keys, bool Truncated);

/// <summary>
///     GitLab's delete-SSH-key endpoints return no body; both fields are the caller's own input, so this is
///     the rare bare-record case.
/// </summary>
public sealed record SshKeyDeletionResult(long KeyId, long? UserId);

public sealed record SshCertificateSummary(long Id, string? Title, string? Key, DateTimeOffset? CreatedAt);

public sealed record SshCertificateListResult(IReadOnlyList<SshCertificateSummary> Certificates, bool Truncated);

public sealed record GpgKeySummary(long Id, string? Key, DateTimeOffset? CreatedAt);

public sealed record GpgKeyListResult(IReadOnlyList<GpgKeySummary> Keys, bool Truncated);

/// <summary>
///     GitLab's delete/revoke GPG key endpoints return no body; every field here is the caller's own input,
///     so this is the rare bare-record case.
/// </summary>
public sealed record GpgKeyManageResult(long KeyId, string Action, long? UserId);

// ---- backlog-split/people/part-4.json below ----

/// <summary>
///     GitLab's delete-service-account endpoints return no body; every field here is the caller's own input
///     (<see cref="Scope" /> is one of the tool's own literals, "project" or "group"), so this is the rare
///     bare-record case (CLAUDE.md rule 4 / mcp-tool-authoring Step 4).
/// </summary>
public sealed record ServiceAccountDeletionResult(long UserId, string Scope, bool HardDelete);