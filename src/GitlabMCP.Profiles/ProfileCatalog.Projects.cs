using GitlabMCP.Abstractions;

namespace GitlabMCP.Profiles;

public static partial class ProfileCatalog
{
    // Projects/groups/namespaces tools (docs/tool-catalog-by-domain.json key "projects"). Grants map
    // each entry's "profiles" array onto Grant flags; Grant.Delivery == Developer|DevOps and
    // Grant.Planning == Maintainer|Developer are the named combinations CLAUDE.md's persona table
    // implies for this domain.
    //
    // Grant.AdminOnly stays bare (never combined with a persona bit) for every row here — each one's
    // own [Description] says "administrator(s) only" / "Administrator token required", i.e. genuinely
    // instance-admin-only. Combining AdminOnly with a persona bit would make the row reachable under
    // that bare persona directly (Grant.IsVisibleIn checks each bit independently, not as an AND-gate),
    // silently defeating that requirement — see DECISIONS.md's review-findings note (18+26).
    // gitlab_list_storage_moves has no such admin-only wording in its own [Description] ("self-managed
    // instances only", not "administrators only") and stays a plain Grant.DevOps operational tool.
    private static IReadOnlyDictionary<string, ToolGrant> ProjectsRows()
    {
        return new Dictionary<string, ToolGrant>(StringComparer.Ordinal)
        {
            ["gitlab_list_group_issues"] = new(Grant.Planning, true),
            ["gitlab_list_project_forks"] = new(Grant.Developer, true),
            ["gitlab_list_namespace_storage_limit_exclusions"] = new(Grant.AdminOnly, true),
            ["gitlab_list_project_uploads"] = new(Grant.Delivery, true),
            ["gitlab_list_project_aliases"] = new(Grant.AdminOnly, true),
            ["gitlab_list_topics"] = new(Grant.Delivery, true),
            ["gitlab_list_badges"] = new(Grant.Delivery, true),
            ["gitlab_list_custom_attributes"] = new(Grant.AdminOnly, true),
            ["gitlab_get_avatar_url_for_email"] = new(Grant.Delivery, true),
            ["gitlab_list_storage_moves"] = new(Grant.DevOps, true),
            ["gitlab_list_group_projects"] = new(Grant.Delivery, true),
            ["gitlab_create_ci_config_merge_request"] = new(Grant.Delivery, false),
            ["gitlab_create_group"] = new(Grant.Delivery, false),
            ["gitlab_delete_topic"] = new(Grant.AdminOnly, false),
            ["gitlab_create_badge"] = new(Grant.Delivery, false),
            ["gitlab_delete_project_upload"] = new(Grant.Delivery, false),
            ["gitlab_create_project_alias"] = new(Grant.AdminOnly, false),
            ["gitlab_set_custom_attribute"] = new(Grant.AdminOnly, false),

            // gitlab_create_project's backlog entry lists profiles [Developer, DevOps, AdminOnly]: the
            // AdminOnly half is the optional ownerUserId path (CreateForUserAsync, "Administrators only"
            // per GitLab.Client's own XML doc on that one dispatch branch), not the tool as a whole — its
            // [Description] does not say "administrators only". Per the AdminOnly-never-combined rule,
            // this row stays a bare persona grant; the ownerUserId branch is left to GitLab's own 403 on a
            // non-admin token (CLAUDE.md: "the GitLab token's own permissions are the real ceiling").
            ["gitlab_get_project"] = new(Grant.Delivery, true),
            ["gitlab_list_projects"] = new(Grant.Delivery, true),
            ["gitlab_create_project"] = new(Grant.Delivery, false),
            ["gitlab_update_project"] = new(Grant.Delivery, false),
            ["gitlab_delete_project"] = new(Grant.Delivery, false),
            ["gitlab_restore_project"] = new(Grant.Delivery, false),
            ["gitlab_set_project_archived"] = new(Grant.Delivery, false),
            ["gitlab_set_project_starred"] = new(Grant.Developer, false),
            ["gitlab_fork_project"] = new(Grant.Developer, false),
            ["gitlab_transfer_project"] = new(Grant.Delivery, false),
            ["gitlab_share_project_with_group"] = new(Grant.Delivery, false),
            ["gitlab_list_project_related_groups"] = new(Grant.Developer, true),
            ["gitlab_list_project_members"] = new(Grant.Planning, true),
            ["gitlab_get_project_insights"] = new(Grant.Delivery, true),
            ["gitlab_manage_project_security_settings"] = new(Grant.DevOps, false),
            ["gitlab_list_user_projects"] = new(Grant.Developer, true),
            ["gitlab_get_group"] = new(Grant.Delivery, true),
            ["gitlab_list_groups"] = new(Grant.Delivery, true),

            // Chunk 2 additions (backlog part-2.json).
            ["gitlab_update_group"] = new(Grant.Delivery, false),
            ["gitlab_delete_group"] = new(Grant.Delivery, false),
            ["gitlab_restore_group"] = new(Grant.Delivery, false),
            ["gitlab_set_group_archived"] = new(Grant.Delivery, false),
            ["gitlab_list_subgroups"] = new(Grant.Delivery, true),
            ["gitlab_move_project_into_group"] = new(Grant.Delivery, false),
            ["gitlab_set_group_share"] = new(Grant.Delivery, false),

            // gitlab_transfer_group, gitlab_list_group_special_users and gitlab_get_group_audit_event
            // all carry ["DevOps","AdminOnly"] in the backlog source, but per the AdminOnly-never-
            // combined rule that pairing is only honoured when the tool's own [Description] actually
            // states "requires administrator access" / "administrators only" / "Administrator token
            // required" (DEC-027). GitLab.Client's own XML docs for the underlying methods say
            // otherwise: IGroupsClient.TransferAsync carries no admin remark at all (Owner-on-both-
            // groups is the real requirement); IGroupsClient.GetAuditEventAsync's doc says "Restricted
            // to group Owners and administrators" (Owners can already reach it); ListBillableMembersAsync/
            // ListProvisionedUsersAsync/ListSamlUsersAsync carry no admin remark either (top-level-group
            // Owner). None of the three descriptions below use a trigger phrase, so all three stay bare
            // Grant.DevOps — a DevOps-profile Owner-scoped token can call them; a non-Owner token gets
            // GitLab's own 403, per CLAUDE.md's "the token is the real ceiling".
            ["gitlab_transfer_group"] = new(Grant.DevOps, false),
            ["gitlab_get_group_issue_statistics"] = new(Grant.Planning, true),
            ["gitlab_list_group_special_users"] = new(Grant.DevOps, true),
            ["gitlab_get_group_audit_event"] = new(Grant.DevOps, true),
            ["gitlab_update_group_security_settings"] = new(Grant.DevOps, false),
            ["gitlab_upload_project_file"] = new(Grant.Delivery, false),
            ["gitlab_download_project_upload"] = new(Grant.Delivery, true),
            ["gitlab_get_namespace"] = new(Grant.Delivery, true),
            ["gitlab_list_namespaces"] = new(Grant.Delivery, true),

            // gitlab_get_namespace_subscription also carries ["DevOps","AdminOnly"] in the backlog
            // source; INamespacesClient.GetSubscriptionAsync's own XML doc states no admin-only
            // requirement (real GitLab behaviour: namespace Owner or administrator), so this stays
            // bare Grant.DevOps for the same reason as the three rows above.
            ["gitlab_get_namespace_subscription"] = new(Grant.DevOps, true),

            // gitlab_set_namespace_storage_limit_exclusion's [Description] states "Administrator token
            // required." (matching the trigger phrase), consistent with its sibling
            // gitlab_list_namespace_storage_limit_exclusions above (storage-limit exclusions are an
            // instance-wide billing control, not a namespace-Owner-reachable one) — bare Grant.AdminOnly.
            ["gitlab_set_namespace_storage_limit_exclusion"] = new(Grant.AdminOnly, false),

            // Chunk 3 additions (backlog part-3.json).

            // gitlab_get_project_alias / gitlab_delete_project_alias: IProjectAliasesClient's own
            // type-level XML doc states "Administrators only, on GitLab Premium and Ultimate" for the
            // whole resource, and both descriptions below say so — matching the trigger phrase and the
            // sibling gitlab_list_project_aliases/gitlab_create_project_alias rows above. Bare AdminOnly.
            ["gitlab_get_project_alias"] = new(Grant.AdminOnly, true),
            ["gitlab_delete_project_alias"] = new(Grant.AdminOnly, false),

            // ITopicsClient's own type-level XML doc: "Listing and reading topics is open to any
            // authenticated user. Creating, updating, deleting and merging them is administrator-only."
            // gitlab_get_topic is a read -> Grant.Delivery, matching gitlab_list_topics above.
            // gitlab_upsert_topic (create/update) and gitlab_merge_topics are administrator-only per
            // that same doc and per ITopicsClient.CreateAsync/UpdateAsync/MergeAsync's own per-method
            // "Administrators only" remarks; both descriptions below say so, matching the sibling
            // gitlab_delete_topic row above (also administrator-only per ITopicsClient.DeleteAsync).
            // Bare AdminOnly on both.
            ["gitlab_get_topic"] = new(Grant.Delivery, true),
            ["gitlab_upsert_topic"] = new(Grant.AdminOnly, false),
            ["gitlab_merge_topics"] = new(Grant.AdminOnly, false),

            // IBadgesClient carries no admin-only remark at the type or method level (unlike topics/
            // custom-attributes/project-aliases above) - matching the sibling gitlab_list_badges/
            // gitlab_create_badge rows, which are plain Grant.Delivery.
            ["gitlab_get_badge"] = new(Grant.Delivery, true),
            ["gitlab_update_badge"] = new(Grant.Delivery, false),
            ["gitlab_delete_badge"] = new(Grant.Delivery, false),

            // ICustomAttributesClient's own type-level XML doc: "Every operation here, reads included,
            // is administrator-only." Both descriptions below say "Administrator token required.",
            // matching the sibling gitlab_list_custom_attributes/gitlab_set_custom_attribute rows above.
            // Bare AdminOnly on both.
            ["gitlab_get_custom_attribute"] = new(Grant.AdminOnly, true),
            ["gitlab_delete_custom_attribute"] = new(Grant.AdminOnly, false),

            // IAvatarsClient carries no admin-only remark - matching the sibling
            // gitlab_get_avatar_url_for_email row, which is plain Grant.Delivery.
            ["gitlab_download_avatar"] = new(Grant.Delivery, true),

            // IStorageMovesClient's own type-level XML doc: "Every endpoint here requires instance
            // administrator rights. A non-administrator token gets 403 Forbidden ... so this resource is
            // unusable against GitLab.com and only meaningful on a self-managed instance." That is
            // stronger than what gitlab_list_storage_moves' own [Description] states above ("self-managed
            // instances only", no admin wording, and so left at bare Grant.DevOps per this file's own
            // precedent) - the description written for THIS tool below does state "Administrator token
            // required", per the same DEC-027 rule ("classification follows the tool's own [Description]
            // wording, verified against the real client doc"), so this row is bare Grant.AdminOnly rather
            // than mirroring its sibling. The two rows intentionally diverge; the sibling is unchanged.
            ["gitlab_create_storage_move"] = new(Grant.AdminOnly, false),

            // IOrganizationsClient's own type/method-level XML docs carry no admin-only remark (unlike
            // topics/custom-attributes/project-aliases/storage-moves above), so both rows below stay
            // bare Grant.DevOps - Organizations is instance/platform-shaped surface ("the newer top-level
            // container GitLab is building above groups") that fits the DevOps persona's Scope
            // (CLAUDE.md: "Administration plus settings ... and the surrounding infrastructure"),
            // matching the treatment already given to gitlab_transfer_group /
            // gitlab_get_namespace_subscription above where the backlog source likewise proposed
            // ["DevOps","AdminOnly"] but the underlying doc did not support the AdminOnly half.
            ["gitlab_create_organization"] = new(Grant.DevOps, false),
            ["gitlab_delete_organization"] = new(Grant.DevOps, false)
        };
    }
}