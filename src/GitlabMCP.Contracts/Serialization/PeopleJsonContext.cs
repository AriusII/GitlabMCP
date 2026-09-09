using System.Text.Json.Serialization;
using GitlabMCP.Contracts.People;

namespace GitlabMCP.Contracts.Serialization;

/// <summary>
///     Source-generated JSON metadata for every People-domain (users/members/tokens/access) payload record.
///     This is a dedicated context, deliberately separate from <c>GitlabMcpJsonContext</c> — see that file's
///     header comment for the verified <c>CS8785</c> generator bug this avoids. Every <c>[JsonSerializable]</c>
///     attribute for the People domain lives in this ONE file; do not split them across a second file of this
///     same partial class.
/// </summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(EnterpriseUserSummary))]
[JsonSerializable(typeof(EnterpriseUserListResult))]
[JsonSerializable(typeof(ImpersonationTokenSummary))]
[JsonSerializable(typeof(ImpersonationTokenListResult))]
[JsonSerializable(typeof(ImpersonationTokenCreated))]
[JsonSerializable(typeof(MemberSummary))]
[JsonSerializable(typeof(MemberListResult))]
[JsonSerializable(typeof(SshKeySummary))]
[JsonSerializable(typeof(SshKeyListResult))]
[JsonSerializable(typeof(ResourceAccessTokenSummary))]
[JsonSerializable(typeof(ResourceAccessTokenListResult))]
[JsonSerializable(typeof(ResourceAccessTokenCreated))]
[JsonSerializable(typeof(ServiceAccountSummary))]
[JsonSerializable(typeof(ServiceAccountListResult))]
[JsonSerializable(typeof(MemberRoleSummary))]
[JsonSerializable(typeof(MemberRoleListResult))]
[JsonSerializable(typeof(AccessRequestSummary))]
[JsonSerializable(typeof(AccessRequestListResult))]
[JsonSerializable(typeof(GroupMemberStateUpdateResult))]
[JsonSerializable(typeof(RunnerRegistrationResult))]
[JsonSerializable(typeof(EnterpriseUserUpdateResult))]
[JsonSerializable(typeof(InvitationResult))]
[JsonSerializable(typeof(InvitationListResult))]
[JsonSerializable(typeof(InvitationDeletionResult))]
[JsonSerializable(typeof(PendingGroupMemberSummary))]
[JsonSerializable(typeof(PendingGroupMemberListResult))]
[JsonSerializable(typeof(ApprovePendingGroupMembersResult))]
[JsonSerializable(typeof(MemberRemovalResult))]
[JsonSerializable(typeof(MemberRoleDeletionResult))]
[JsonSerializable(typeof(AccessRequestReviewResult))]
[JsonSerializable(typeof(SamlGroupLinkSummary))]
[JsonSerializable(typeof(SamlGroupLinkListResult))]
[JsonSerializable(typeof(SamlGroupLinkDeletionResult))]
[JsonSerializable(typeof(ProviderIdentitySummary))]
[JsonSerializable(typeof(ProviderIdentityListResult))]
// backlog-split/people/part-2.json below
[JsonSerializable(typeof(ProviderIdentityManageResult))]
[JsonSerializable(typeof(NotificationSettingsResult))]
[JsonSerializable(typeof(CurrentUserResult))]
[JsonSerializable(typeof(UserCountsSummary))]
[JsonSerializable(typeof(EmailSummary))]
[JsonSerializable(typeof(CurrentUserEmailResult))]
[JsonSerializable(typeof(UserPreferencesResult))]
[JsonSerializable(typeof(UserStatusResult))]
[JsonSerializable(typeof(SupportPinResult))]
[JsonSerializable(typeof(UserListResult))]
[JsonSerializable(typeof(UserManageResult))]
[JsonSerializable(typeof(UserDeletionResult))]
[JsonSerializable(typeof(UserAccountStateResult))]
[JsonSerializable(typeof(PersonalAccessTokenSummary))]
[JsonSerializable(typeof(PersonalAccessTokenListResult))]
[JsonSerializable(typeof(PersonalAccessTokenCreated))]
// backlog-split/people/part-3.json below
[JsonSerializable(typeof(PersonalAccessTokenRevocationResult))]
[JsonSerializable(typeof(ImpersonationTokenRevocationResult))]
[JsonSerializable(typeof(ResourceAccessTokenRevocationResult))]
[JsonSerializable(typeof(ServiceAccountAccessTokenRevocationResult))]
[JsonSerializable(typeof(GroupCredentialManageResult))]
[JsonSerializable(typeof(GroupManagedSshKeySummary))]
[JsonSerializable(typeof(GroupManagedSshKeyListResult))]
[JsonSerializable(typeof(SshKeyDeletionResult))]
[JsonSerializable(typeof(SshCertificateSummary))]
[JsonSerializable(typeof(SshCertificateListResult))]
[JsonSerializable(typeof(GpgKeySummary))]
[JsonSerializable(typeof(GpgKeyListResult))]
[JsonSerializable(typeof(GpgKeyManageResult))]
// backlog-split/people/part-4.json below
[JsonSerializable(typeof(ServiceAccountDeletionResult))]
public sealed partial class PeopleJsonContext : JsonSerializerContext;