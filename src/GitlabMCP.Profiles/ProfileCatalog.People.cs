using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // People domain (users, members, tokens, access).
    //
    // Grant.AdminOnly is bare (never combined with a persona bit) except where a tool is genuinely meant
    // to be reachable under a persona too — combining them is otherwise inert-and-misleading:
    // Grant.IsVisibleIn checks each bit independently, so e.g. "Grant.DevOps | Grant.AdminOnly" is
    // reachable under bare McpProfile.DevOps already (the DevOps bit alone satisfies it), which silently
    // defeats "requires instance administrator rights" wording in a tool's own [Description]. See
    // DECISIONS.md's review-findings note (18+26). Below, a tool whose [Description] says "requires
    // instance administrator rights" (gitlab_list_service_accounts, gitlab_list_member_roles) is bare
    // Grant.AdminOnly; "Administrators and group owners only" (the two enterprise-user tools) is
    // group-owner-reachable, not instance-admin-only, so it stays a persona bit; everything else here has
    // no admin-only language in its own [Description] and is a plain project/group-scoped operation, so
    // the AdminOnly bit some of these previously carried was dropped as redundant with the persona bit
    // already covering FullPermission via "grant != Grant.None".
    private static IReadOnlyDictionary<string, ToolGrant> PeopleRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_enterprise_users"] = new(Grant.DevOps, true),
            ["gitlab_list_impersonation_tokens"] = new(Grant.AdminOnly, true),
            ["gitlab_list_members"] = new(Grant.Everyone, true),
            ["gitlab_list_ssh_keys"] = new(Grant.Everyone, true),
            ["gitlab_list_resource_access_tokens"] = new(Grant.DevOps, true),
            ["gitlab_list_service_accounts"] = new(Grant.AdminOnly, true),
            ["gitlab_list_member_roles"] = new(Grant.AdminOnly, true),
            ["gitlab_list_access_requests"] = new(Grant.Everyone, true),
            ["gitlab_update_group_member_state"] = new(Grant.DevOps, false),
            ["gitlab_create_impersonation_token"] = new(Grant.AdminOnly, false),
            ["gitlab_create_current_user_runner"] = new(Grant.DevOps, false),
            ["gitlab_update_enterprise_user"] = new(Grant.DevOps, false),
            ["gitlab_create_invitation"] = new(Grant.Delivery, false),
            ["gitlab_create_resource_access_token"] = new(Grant.DevOps, false),

            // Backlog chunk 2: every one of these lists Grant.AdminOnly alongside a persona bit in its
            // own backlog "profiles" array, but none of their [Description] text says "administrators
            // only" / "requires administrator access" — so per the rule above, and DEC-027, the AdminOnly
            // bit is dropped as redundant: FullPermission already sees any of these via "grant !=
            // Grant.None", and keeping AdminOnly here would change nothing except invite the next reader
            // to think it does something.
            ["gitlab_get_member"] = new(Grant.Everyone, true),
            ["gitlab_add_member"] = new(Grant.Delivery, false),
            ["gitlab_update_member"] = new(Grant.Delivery, false),
            ["gitlab_remove_member"] = new(Grant.DevOps, false),
            ["gitlab_list_pending_group_members"] = new(Grant.DevOps, true),
            ["gitlab_approve_pending_group_members"] = new(Grant.DevOps, false),
            ["gitlab_set_group_member_ldap_override"] = new(Grant.DevOps, false),
            ["gitlab_create_member_role"] = new(Grant.DevOps, false),
            ["gitlab_delete_member_role"] = new(Grant.DevOps, false),
            ["gitlab_request_access"] = new(Grant.Everyone, false),
            ["gitlab_review_access_request"] = new(Grant.DevOps, false),
            ["gitlab_list_invitations"] = new(Grant.Everyone, true),
            ["gitlab_update_invitation"] = new(Grant.DevOps, false),
            ["gitlab_delete_invitation"] = new(Grant.DevOps, false),
            ["gitlab_list_saml_group_links"] = new(Grant.DevOps, true),
            ["gitlab_create_saml_group_link"] = new(Grant.DevOps, false),
            ["gitlab_delete_saml_group_link"] = new(Grant.DevOps, false),
            ["gitlab_list_group_provider_identities"] = new(Grant.DevOps, true),

            // Backlog-split/people/part-2.json. Every one of these lists Grant.AdminOnly alongside a
            // persona bit in its own backlog "profiles" array too, but per DEC-027 the AdminOnly bit is
            // kept ONLY where the tool's own [Description] states "administrators only" as a blanket
            // requirement for the whole tool (verified against GitLab.Client's XML doc, which says so
            // explicitly for every IUsersClient write except follow/unfollow): gitlab_manage_user,
            // gitlab_delete_user, and gitlab_set_user_account_state are bare Grant.AdminOnly. Everything
            // else here is either genuinely self-scoped (the caller's own notification settings, email,
            // preferences, status, support PIN, personal access tokens) or a read usable by any persona
            // (get/list users, get user status), so the AdminOnly bit is dropped as redundant with the
            // persona bit already covering FullPermission via "grant != Grant.None".
            // gitlab_create_personal_access_token is the one dispatch tool that is *partly* admin-gated
            // (creating for another user, via CreateForUserAsync, is "Administrators only" per the XML)
            // but the tool's own [Description] does not claim the whole tool requires administrator
            // access — self-creation (CreateForCurrentUserAsync) works for any persona — so it keeps its
            // persona bits alone per the same rule, and the token's own real permission ceiling (DEC per
            // CLAUDE.md "the token is the real ceiling") is what actually blocks a non-admin from using
            // the userId parameter.
            ["gitlab_manage_group_provider_identity"] = new(Grant.DevOps, false),
            ["gitlab_get_notification_settings"] = new(Grant.Everyone, true),
            ["gitlab_update_notification_settings"] = new(Grant.Everyone, false),
            ["gitlab_get_current_user"] = new(Grant.Everyone, true),
            ["gitlab_manage_current_user_email"] = new(Grant.Everyone, false),
            ["gitlab_manage_current_user_preferences"] = new(Grant.Everyone, false),
            ["gitlab_manage_current_user_status"] = new(Grant.Everyone, false),
            ["gitlab_manage_current_user_support_pin"] = new(Grant.Everyone, false),
            ["gitlab_get_user"] = new(Grant.Everyone, true),
            ["gitlab_list_users"] = new(Grant.Everyone, true),
            ["gitlab_manage_user"] = new(Grant.AdminOnly, false),
            ["gitlab_delete_user"] = new(Grant.AdminOnly, false),
            ["gitlab_set_user_account_state"] = new(Grant.AdminOnly, false),
            ["gitlab_set_user_follow"] = new(Grant.Everyone, false),
            ["gitlab_get_user_status"] = new(Grant.Everyone, true),
            ["gitlab_list_personal_access_tokens"] = new(Grant.Everyone, true),
            ["gitlab_get_personal_access_token"] = new(Grant.Everyone, true),
            ["gitlab_create_personal_access_token"] = new(Grant.Everyone, false),

            // Backlog-split/people/part-3.json. Same rule as above: bare Grant.AdminOnly is kept only
            // where the tool's own [Description] states the whole tool is administrators-only
            // (gitlab_revoke_impersonation_token, gitlab_find_ssh_key_owner — both verified against
            // GitLab.Client's XML doc, which says so explicitly). The two group-credentials-inventory
            // tools are NOT in that bucket: their [Description] reads "requires ... the caller to be a
            // group Owner or instance administrator" — the identical OR-eligibility shape as the
            // enterprise-user tools above ("Administrators and group owners only"), which this file
            // already keeps as a persona bit rather than bare AdminOnly. So they carry Grant.DevOps too:
            // a bare-AdminOnly row would make them invisible to the very group Owners their own
            // [Description] says qualify. Every other row here lists AdminOnly alongside a persona bit
            // in its own backlog "profiles" array but its [Description] does not claim the whole tool is
            // admin-only (only one dispatch branch is, e.g. GetByIdAsync/CreateForUserAsync-shaped "as an
            // administrator" branches) — so per DEC-027 the AdminOnly bit is dropped as redundant: the
            // persona bit(s) already reach FullPermission via "grant != Grant.None".
            ["gitlab_rotate_personal_access_token"] = new(Grant.Everyone, false),
            ["gitlab_revoke_personal_access_token"] = new(Grant.Everyone, false),
            ["gitlab_revoke_impersonation_token"] = new(Grant.AdminOnly, false),
            ["gitlab_get_resource_access_token"] = new(Grant.DevOps, true),
            ["gitlab_rotate_resource_access_token"] = new(Grant.DevOps, false),
            ["gitlab_revoke_resource_access_token"] = new(Grant.DevOps, false),
            ["gitlab_list_service_account_access_tokens"] = new(Grant.DevOps, true),
            ["gitlab_manage_service_account_access_token"] = new(Grant.DevOps, false),
            ["gitlab_list_group_credential_inventory"] = new(Grant.DevOps, true),
            ["gitlab_manage_group_credential"] = new(Grant.DevOps, false),
            ["gitlab_get_ssh_key"] = new(Grant.Everyone, true),
            ["gitlab_add_ssh_key"] = new(Grant.Everyone, false),
            ["gitlab_delete_ssh_key"] = new(Grant.Delivery, false),
            ["gitlab_find_ssh_key_owner"] = new(Grant.AdminOnly, true),
            ["gitlab_manage_group_ssh_certificates"] = new(Grant.DevOps, false),
            ["gitlab_list_gpg_keys"] = new(Grant.Everyone, true),
            ["gitlab_add_gpg_key"] = new(Grant.Delivery, false),
            ["gitlab_manage_gpg_key"] = new(Grant.Delivery, false),

            // Backlog-split/people/part-4.json. gitlab_get_service_account and
            // gitlab_manage_service_account each list Grant.AdminOnly alongside Grant.DevOps in their own
            // backlog "profiles" array, but neither tool's own [Description] claims the whole tool is
            // administrators-only (get is group-Owner/project-Owner-or-Maintainer scoped; manage's
            // instance-scope branch alone needs admin rights, the same partial-admin shape
            // gitlab_create_personal_access_token already has above) — so per DEC-027 the AdminOnly bit is
            // dropped as redundant with the persona bit already covering FullPermission via "grant !=
            // Grant.None". gitlab_delete_service_account's own backlog "profiles" array names AdminOnly
            // alone with no persona bit at all (unlike its two siblings above) — the one explicit signal in
            // this whole file that a tool should NOT be bare-DevOps-reachable, so its [Description] states
            // "Requires administrator access" and it keeps bare Grant.AdminOnly.
            ["gitlab_get_service_account"] = new(Grant.DevOps, true),
            ["gitlab_manage_service_account"] = new(Grant.DevOps, false),
            ["gitlab_delete_service_account"] = new(Grant.AdminOnly, false)
        };
    }
}