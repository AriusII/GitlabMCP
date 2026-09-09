# GitlabMCP tool catalog

**Owner:** DEC-025 (`DECISIONS.md`). This is the authoritative, implemented tool list.

## Status: fully implemented — original 180-tool slice plus the full 542-tool backlog (2026-09-09)

**731 tools are live**, registered, and confirmed reachable:
- **723 REST tools** across 13 domains — the *entire* 722-candidate catalog the research pass originally
  verified against the real `GitLab.Client` 1.0.0 XML, plus one gap the post-implementation adversarial
  review found and filled (`gitlab_list_discussion_notes` — no tool could page a discussion thread's notes
  past `DiscussionMapper`'s 20-note cap; see `docs/review-findings.md`). DEC-025 first implemented a
  180-tool slice (13 parallel domain agents, each re-verifying every `(client, method)` pair against the
  XML **and** by reflecting over the real `GitLab.Client.dll`); a second wave implemented the remaining 542
  (`tool-catalog-backlog.json`) against the identical verification discipline, chunked ~18 tools per
  agent call, sequential within a domain to avoid concurrent-file edits, 13 domains in parallel. Zero
  substitutions, zero skips — all 542 backlog items were implemented, none left unimplemented.
- **7 epic/work-item tools** (DEC-011=C/DEC-020/DEC-021) on `src/GitlabMCP.GraphQL/`, hand-designed against a live GitLab.com GraphQL schema introspection.
- **1 canary tool** (`gitlab_ping`) with no GitLab dependency, used to validate the host/profile-gate/JSON pipeline end to end before the domain implementation pass.

Also live: **9 prompts** and **2 direct + 7 templated resources** (DEC-029/DEC-030/DEC-031) — see
`docs/architecture.md`'s "Prompt and resource surface" section; this file's scope is tools only.

**Verification performed, not merely claimed:**
1. `dotnet build GitlabMCP.slnx -c Release` → **0 warnings, 0 errors** across all 7 `src/*` projects, both
   on the original 188-tool merge and again after the 542-tool backlog wave (a build-fix loop ran 3
   iterations to reach clean on the backlog wave — see `journal.jsonl` of that workflow run for the
   specific errors it corrected).
2. The original 188 tools were individually `tools/call`-ed against a running server (DEC-007's mandatory
   pass; see the per-tool result breakdown this file used to carry for that slice, now superseded by the
   totals below) with **zero SDK-redacted generic failures**. The 542-tool backlog wave was verified by an
   independent adversarial review pass (13 domains × 5 dimensions: secret-field leakage, envelope
   correctness, validation/hints, correctness/quality, profile-grant correctness) plus a build-clean gate,
   rather than a second individual per-tool call sweep — a full 731-tool call sweep has not been re-run
   as one pass this session; treat the backlog wave's tools as build-verified and review-verified, not
   individually call-verified, until such a sweep is run.
3. `dotnet publish -c Release -r win-x64` → **0 warnings**, `Generating native code` present, exit 0,
   re-run and re-verified after the backlog wave and again after the prompt/resource surface landed. The
   published single-file executable was run standalone and answered a real `tools/list` (731 tools),
   `resources/read` and `prompts/get` over HTTP.
4. `docker build` for `linux-musl-x64` (the real container target) — see `docker-aot-image` skill / the Dockerfile for the current result.

Ground truth: `tests/snapshots/<profile>.tools.txt` (sorted names, one file per profile) — captured live
from a running server, not hand-maintained. `tool-catalog-selected.json` (the original 180) and
`tool-catalog-backlog.json` (the 542 that were backlog and are now implemented) remain as the historical
record of the original research pass and selection method, not a pending work list.

## Selection method (DEC-025)

Proportional per-domain targets (13 domains), a ~60/40 read/write split per domain, and round-robin selection across resource clients within a domain for diversity — never concentrating a domain's slice on one client. Verb priority within the read and write pools favours `list`/`get` and `create`/`update` over rarer admin-toggle verbs. Every domain and all four profiles (`Maintainer`, `Developer`, `DevOps`, `AdminOnly`) are represented; nothing is architecturally thin.

**Totals:** 180 tools · 108 read-only (60%) · 8 destructive · 64 other write.

## Wire naming (DEC-022)

Every tool's `Name` is `gitlab_<verb>_<noun>`, set explicitly at registration — never the SDK-derived name.

## Catalog by domain

### Planning & work tracking (`planning`) — 20 tools, indicative profiles: Maintainer + Developer

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_board` | `IBoardsClient::CreateForProjectAsync, CreateForGroupAsync` | WR | Maintainer, Developer | Create a new issue board. |
| `gitlab_create_issue` | `IIssuesClient::CreateAsync` | WR | Maintainer, Developer | Create a new issue with title, description, labels, assignees, milestone, due/start date, weight, confidentiality and type. |
| `gitlab_create_label` | `ILabelsClient::CreateAsync, CreateForGroupAsync` | WR | Maintainer, Developer | Create a label with a name and color (and, at project scope, a priority) at project or group scope. |
| `gitlab_create_milestone` | `IMilestonesClient::CreateAsync, CreateForGroupAsync` | WR | Maintainer, Developer | Create a milestone with a title, description, and start/due dates at project or group scope. |
| `gitlab_create_todo` | `ITodosClient::CreateForIssueAsync, CreateForMergeRequestAsync` | WR | Maintainer, Developer | Create a to-do item for yourself on an issue or merge request. |
| `gitlab_get_label` | `ILabelsClient::GetAsync, GetForGroupAsync` | RO | Maintainer, Developer | Get one label by name (or numeric id) at project or group scope. |
| `gitlab_list_board_lists` | `IBoardsClient::ListListsForProjectAsync, ListListsForGroupAsync` | RO | Maintainer, Developer | List a board's lists (columns) in board order. |
| `gitlab_list_boards` | `IBoardsClient::ListForProjectAsync, ListForGroupAsync` | RO | Maintainer, Developer | List a project's or group's issue boards. |
| `gitlab_list_issue_links` | `IIssuesClient::ListLinksAsync` | RO | Maintainer, Developer | List the issues linked to this one (relates_to / blocks / is_blocked_by), each carrying its link id and link type. |
| `gitlab_list_issue_participants` | `IIssuesClient::ListParticipantsAsync` | RO | Maintainer, Developer | List the users participating in an issue's discussion. |
| `gitlab_list_iterations` | `IIterationsClient::ListForGroupAsync, ListForProjectAsync` | RO | Maintainer, Developer | List the iterations visible to a group or project. Read-only: iterations are scheduled through cadences the REST API doesn't expose here; a project's iterations actually come from its ancestor groups. |
| `gitlab_list_labels` | `ILabelsClient::ListAsync, ListForGroupAsync` | RO | Maintainer, Developer | List a project's or group's labels, optionally with issue/MR counts. |
| `gitlab_list_milestone_burndown_events` | `IMilestonesClient::ListBurndownEventsAsync, ListBurndownEventsForGroupAsync` | RO | Maintainer, Developer | List the burndown chart events of a milestone. Premium/Ultimate feature; expect a 403 on lower plans. |
| `gitlab_list_milestone_issues` | `IMilestonesClient::ListIssuesAsync, ListIssuesForGroupAsync` | RO | Maintainer, Developer | List every issue assigned to a milestone. |
| `gitlab_list_resource_events` | `IResourceEventsClient::ListIssueLabelEventsAsync, ListIssueStateEventsAsync, ListIssueMilestoneEventsAsync, ListIssueIterationEventsAsync, ListIssueWeightEventsAsync, ListMergeRequestLabelEventsAsync, ListMergeRequestStateEventsAsync, ListMergeRequestMilestoneEventsAsync, ListEpicLabelEventsAsync, ListEpicStateEventsAsync` | RO | Maintainer, Developer | List the machine-readable change history (label, state, milestone, iteration, or weight events) of an issue, merge request, or epic. Not every family exists for every eventable type: issues have all five, merge requests have label/state/milestone only, epics have label/state only. |
| `gitlab_list_resource_groups` | `IResourceGroupsClient::ListAsync` | RO | Developer, DevOps | List the CI/CD resource groups (deployment-concurrency mutexes) a project's pipelines have created. |
| `gitlab_list_todos` | `ITodosClient::ListAsync` | RO | Maintainer, Developer | List the authenticated user's to-do items, filterable by action, author, project, group, state, or type. Pending items only with no filters. |
| `gitlab_set_issue_subscription` | `IResourceSubscriptionsClient::SubscribeToIssueAsync, UnsubscribeFromIssueAsync` | WR | Maintainer, Developer | Subscribe or unsubscribe the authenticated user to an issue's notifications. |
| `gitlab_set_resource_group_process_mode` | `IResourceGroupsClient::UpdateAsync` | WR | Developer, DevOps | Change the order in which a resource group releases queued jobs (e.g. unordered / oldest_first / newest_first). |
| `gitlab_update_issue` | `IIssuesClient::UpdateAsync` | WR | Maintainer, Developer | Apply a partial update to an issue's fields (title, description, labels, assignees, milestone, weight, severity, confidentiality, dates) without touching its open/closed state. |

### Notes, discussions & reactions (`discussion`) — 12 tools, indicative profiles: Maintainer + Developer

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_add_reaction` | `IAwardEmojiClient::AddToIssueAsync / AddToIssueNoteAsync / AddToMergeRequestAsync / AddToMergeRequestNoteAsync / AddToSnippetAsync / AddToSnippetNoteAsync / AddToEpicAsync / AddToEpicNoteAsync (dispatched by target_type)` | WR | Maintainer, Developer | React with an emoji (name without colons) to an issue/MR/snippet/epic or a note on one. |
| `gitlab_apply_suggestion` | `ISuggestionsClient::ApplyAsync` | DX | Developer | Apply one code suggestion from a review comment, committing it to the merge request's source branch. Caller must already have the suggestion id (not discoverable via this library's note model). |
| `gitlab_create_discussion` | `IDiscussionsClient::CreateForIssueAsync / CreateForMergeRequestAsync / CreateForCommitAsync / CreateForSnippetAsync / CreateForEpicAsync (dispatched by noteable_type)` | WR | Maintainer, Developer | Start a new resolvable discussion thread; on merge_request/commit, optionally anchor it to a diff line as a review comment. |
| `gitlab_create_draft_note` | `IDraftNotesClient::CreateAsync` | WR | Developer | Stage a review comment on a merge request without publishing it yet, optionally as a reply or resolving a discussion on publish. |
| `gitlab_get_discussion` | `IDiscussionsClient::GetForIssueAsync / GetForMergeRequestAsync / GetForCommitAsync / GetForSnippetAsync / GetForEpicAsync (dispatched by noteable_type)` | RO | Maintainer, Developer | Read one discussion thread and every note inside it in one call. |
| `gitlab_list_discussions` | `IDiscussionsClient::ListForIssueAsync / ListForMergeRequestAsync / ListForCommitAsync / ListForSnippetAsync / ListForEpicAsync (dispatched by noteable_type)` | RO | Maintainer, Developer | List the threaded, resolvable discussion threads on an issue, MR, commit, snippet or epic (each entry includes its notes), bounded by limit. |
| `gitlab_list_draft_notes` | `IDraftNotesClient::ListAsync` | RO | Developer | List the caller's own pending, unpublished review comments on a merge request. |
| `gitlab_list_events` | `IEventsClient::ListAsync / ListForProjectAsync / ListForUserAsync (dispatched by scope: me/project/user)` | RO | Maintainer, Developer | List recent activity-feed events for the caller, a project, or a user (not an audit log - excludes epic/MR events and is retention-limited). |
| `gitlab_list_notes` | `INotesClient::ListIssueNotesAsync / ListMergeRequestNotesAsync / ListSnippetNotesAsync / ListVulnerabilityNotesAsync / ListProjectWikiPageNotesAsync / ListEpicNotesAsync / ListGroupWikiPageNotesAsync (dispatched by noteable_type)` | RO | Maintainer, Developer | List the flat comments/system-notes on an issue, merge request, snippet, vulnerability, wiki page or epic, bounded by limit. |
| `gitlab_list_reactions` | `IAwardEmojiClient::ListForIssueAsync / ListForIssueNoteAsync / ListForMergeRequestAsync / ListForMergeRequestNoteAsync / ListForSnippetAsync / ListForSnippetNoteAsync / ListForEpicAsync / ListForEpicNoteAsync (dispatched by target_type)` | RO | Maintainer, Developer | List the emoji reactions on an issue, MR, snippet, epic, or a note on any of them. |
| `gitlab_render_markdown` | `IMarkdownClient::RenderAsync` | RO | Maintainer, Developer | Preview how GitLab Flavored Markdown (or plain CommonMark) will render to HTML, optionally resolving #/!/@/~ references against a project. |
| `gitlab_update_note` | `INotesClient::UpdateIssueNoteAsync / UpdateMergeRequestNoteAsync / UpdateSnippetNoteAsync / UpdateVulnerabilityNoteAsync / UpdateProjectWikiPageNoteAsync / UpdateEpicNoteAsync / UpdateGroupWikiPageNoteAsync (dispatched by noteable_type)` | DX | Maintainer, Developer | Replace the body of an existing note the caller authored (or has maintainer rights over). |

### Merge requests & approvals (`mergerequests`) — 18 tools, indicative profiles: Developer

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_add_to_merge_train` | `IMergeTrainsClient::AddMergeRequestAsync` | WR | Developer, DevOps | Queue a merge request onto its target branch's merge train. |
| `gitlab_approve_merge_request` | `IMergeRequestApprovalsClient::ApproveAsync` | WR | Developer | Approve a merge request as the authenticated user. |
| `gitlab_create_approval_rule` | `IApprovalRulesClient::CreateForProjectAsync` | WR | Developer | Create a standing approval rule on a project or group with named approvers/groups and a required approval count. |
| `gitlab_create_merge_request` | `IMergeRequestsClient::CreateAsync` | WR | Developer | Open a new merge request from a source branch to a target branch with a title/description. |
| `gitlab_get_approval_settings` | `IMergeRequestApprovalsClient::GetApprovalConfigurationAsync` | RO | Developer | Read the approval policy switches for a project or group -- author self-approval, committer approval, reset-approvals-on-push, and similar governance flags. |
| `gitlab_get_merge_request_approvals` | `IMergeRequestApprovalsClient::GetApprovalsAsync` | RO | Developer | Check whether a merge request is approved, by whom, and which approval rules remain outstanding -- combines the approvers view (GetApprovalsAsync) and the rule-by-rule view (GetApprovalStateAsync) in one call. |
| `gitlab_list_approval_rules` | `IApprovalRulesClient::ListForProjectAsync` | RO | Developer | List the standing approval rules configured on a project or group, to see what approvals a merge request will need before it can merge. |
| `gitlab_list_external_status_checks` | `IExternalStatusChecksClient::ListAsync` | RO | Developer, DevOps | List the external status checks configured on a project, or (given a merge_request_iid) the live results of those checks against one merge request. |
| `gitlab_list_group_merge_requests` | `IMergeRequestsClient::ListForGroupAsync` | RO | Developer | List merge requests across a group and all of its subgroups for a cross-project view. |
| `gitlab_list_merge_request_commits` | `IMergeRequestsClient::ListCommitsAsync` | RO | Developer | List the commits that make up a merge request. |
| `gitlab_list_merge_request_pipelines` | `IMergeRequestsClient::ListPipelinesAsync` | RO | Developer | List CI pipelines that have run for a merge request, to check merge-gate status before merging. |
| `gitlab_list_merge_request_related_issues` | `IMergeRequestsClient::ListClosesIssuesAsync` | RO | Developer | List the issues a merge request will close on merge, or (via a relation switch) every issue it references, for traceability between planning and code. |
| `gitlab_list_merge_request_reviewers` | `IMergeRequestsClient::ListReviewersAsync` | RO | Developer | List a merge request's reviewers and each one's review state (unreviewed, reviewed, requested changes). |
| `gitlab_list_merge_requests` | `IMergeRequestsClient::ListAsync` | RO | Developer | List a project's merge requests filtered by state/labels/branch/author/search, bounded by limit, for triage and status overview. |
| `gitlab_list_merge_train` | `IMergeTrainsClient::ListAsync` | RO | Developer, DevOps | List the merge requests currently queued on a project's merge train(s), optionally filtered to one target branch. |
| `gitlab_unapprove_merge_request` | `IMergeRequestApprovalsClient::UnapproveAsync` | WR | Developer | Withdraw the authenticated user's own approval from a merge request. |
| `gitlab_update_approval_rule` | `IApprovalRulesClient::UpdateForProjectAsync` | WR | Developer | Update a standing approval rule's approvers or required count; approver lists sent are authoritative and replace the existing list. |
| `gitlab_update_merge_request` | `IMergeRequestsClient::UpdateAsync` | WR | Developer | Edit a merge request's title, description, labels, assignees, reviewers or milestone, or close/reopen it via the state event. |

### Repository, files, branches, commits (`code`) — 16 tools, indicative profiles: Developer (write) / Maintainer (read)

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_branch` | `IBranchesClient::CreateAsync` | WR | Developer | Create a branch from a ref (branch, tag or commit SHA). |
| `gitlab_create_commit` | `ICommitsClient::CreateAsync` | WR | Developer | Commit several file create/update/delete/move/chmod actions atomically in one push, without a local git checkout. |
| `gitlab_create_file` | `IRepositoryFilesClient::CreateAsync` | WR | Developer | Create a new file and commit it directly to a branch, no local checkout needed. |
| `gitlab_create_tag` | `ITagsClient::CreateAsync` | WR | Developer | Tag a ref, optionally as an annotated tag carrying a release message. |
| `gitlab_get_file_blame` | `IRepositoryFilesClient::GetBlameAsync` | RO | Maintainer, Developer | Find which commit and author last touched each line of a file, optionally scoped to a line range. |
| `gitlab_get_pull_mirror` | `IProjectMirrorsClient::GetAsync` | RO | Developer, DevOps | Check a project's pull-mirror configuration and last sync status. |
| `gitlab_get_push_rule` | `IPushRulesClient::GetForProjectAsync` | RO | Developer, DevOps | Read a project's push rule — commit-message/branch-name regexes, secret detection, signing requirements. |
| `gitlab_list_branches` | `IBranchesClient::ListAsync` | RO | Maintainer, Developer | List a project's branches, optionally filtered by name or regex. |
| `gitlab_list_commit_comments` | `ICommitsClient::ListCommentsAsync` | RO | Maintainer, Developer | Read line- or commit-level comments left on a commit. |
| `gitlab_list_commit_statuses` | `ICommitStatusesClient::ListAsync` | RO | Developer, DevOps | See what external CI systems have reported for a commit. |
| `gitlab_list_protected_branches` | `IProtectedBranchesClient::ListAsync` | RO | Maintainer, Developer, DevOps | See which branches or wildcard patterns are protected and who may push, merge or unprotect them. |
| `gitlab_list_protected_tags` | `IProtectedTagsClient::ListAsync` | RO | Maintainer, Developer, DevOps | See which tags or wildcard patterns are protected and who may create matching tags. |
| `gitlab_list_repository_contributors` | `IRepositoriesClient::ListContributorsAsync` | RO | Maintainer, Developer | Per-author commit and line-change counts, for activity or ownership questions. |
| `gitlab_list_tags` | `ITagsClient::ListAsync` | RO | Maintainer, Developer | List a project's tags. |
| `gitlab_protect_branch` | `IProtectedBranchesClient::ProtectAsync` | WR | DevOps | Protect a branch or wildcard pattern with role-, user- or group-grained push/merge/unprotect rules. |
| `gitlab_protect_tag` | `IProtectedTagsClient::ProtectAsync` | WR | DevOps | Protect a tag or wildcard pattern so only specific roles may create matching tags. |

### Users, members, tokens, access (`people`) — 14 tools, indicative profiles: Maintainer read / DevOps write / AdminOnly

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_current_user_runner` | `ICurrentUserClient::CreateRunnerAsync` | WR | DevOps, AdminOnly | Register a new CI/CD runner owned by the caller; the returned registration token is shown once and must be persisted immediately. |
| `gitlab_create_impersonation_token` | `IPersonalAccessTokensClient::CreateImpersonationTokenAsync` | WR | AdminOnly | Create a token that acts as another user for both API calls and Git — the most privileged credential this server can mint; the plaintext secret is shown only once. |
| `gitlab_create_invitation` | `IInvitationsClient::CreateForProjectAsync` | WR | Developer, DevOps, AdminOnly | Invite one or more people, by email or user id, to a project or group at a given access level. |
| `gitlab_create_resource_access_token` | `IAccessTokensClient::CreateForProjectAsync` | WR | DevOps, AdminOnly | Create a project or group access token with chosen scopes and access level; the plaintext token is returned only once. |
| `gitlab_list_access_requests` | `IAccessRequestsClient::ListForProjectAsync` | RO | Maintainer, Developer, DevOps, AdminOnly | List pending access requests on a project or group. |
| `gitlab_list_enterprise_users` | `IUsersClient::ListEnterpriseUsersAsync` | RO | DevOps, AdminOnly | List, or get one by id, the SAML/SCIM-provisioned enterprise users claimed by a group. |
| `gitlab_list_impersonation_tokens` | `IPersonalAccessTokensClient::ListImpersonationTokensAsync` | RO | AdminOnly | List, or get one by id, a user's impersonation tokens. |
| `gitlab_list_member_roles` | `IMemberRolesClient::ListAsync` | RO | Maintainer, DevOps, AdminOnly | List custom member roles at instance, group, or instance-admin-role scope. |
| `gitlab_list_members` | `IMembersClient::ListAsync` | RO | Maintainer, Developer, DevOps, AdminOnly | List a project's or group's members, direct or including inherited/invited-group memberships, bounded by limit. |
| `gitlab_list_resource_access_tokens` | `IAccessTokensClient::ListForProjectAsync` | RO | DevOps, AdminOnly | List a project's or group's access tokens (bot-account tokens scoped to that resource). |
| `gitlab_list_service_accounts` | `IServiceAccountsClient::ListAsync` | RO | DevOps, AdminOnly | List service accounts at instance, group, or project scope. |
| `gitlab_list_ssh_keys` | `ISshKeysClient::ListForCurrentUserAsync` | RO | Maintainer, Developer, DevOps, AdminOnly | List the caller's SSH keys, or another user's (served without authentication by GitLab). |
| `gitlab_update_enterprise_user` | `IUsersClient::UpdateEnterpriseUserAsync` | WR | DevOps, AdminOnly | Update an enterprise user's name, email or project limit, or disable their two-factor authentication. |
| `gitlab_update_group_member_state` | `IMembersClient::UpdateStateForGroupAsync` | WR | DevOps, AdminOnly | Move a user's group membership between Awaiting and Active state to free or reclaim a paid seat. |

### Projects, groups, namespaces (`projects`) — 18 tools, indicative profiles: Developer + DevOps

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_badge` | `IBadgesClient::CreateForProjectAsync` | WR | Developer, DevOps | Add a badge to a project or group (wraps CreateForGroupAsync); group badges are inherited by every project under it. |
| `gitlab_create_ci_config_merge_request` | `IProjectsClient::CreateCiConfigMergeRequestAsync` | WR | Developer, DevOps | Open a merge request that adds a starter .gitlab-ci.yml to a project with none. |
| `gitlab_create_group` | `IGroupsClient::CreateAsync` | WR | Developer, DevOps | Create a top-level group, or a subgroup when a parent group is given. |
| `gitlab_create_project_alias` | `IProjectAliasesClient::CreateAsync` | WR | DevOps, AdminOnly | Create an alias so an imported project keeps answering to its old Git name. |
| `gitlab_delete_project_upload` | `IProjectUploadsClient::DeleteAsync` | DX | Developer, DevOps | Delete a project upload by numeric id or by secret+filename (wraps DeleteBySecretAsync). |
| `gitlab_delete_topic` | `ITopicsClient::DeleteAsync` | DX | DevOps, AdminOnly | Delete a topic; projects that carried it simply lose the label. |
| `gitlab_get_avatar_url_for_email` | `IAvatarsClient::GetForEmailAsync` | RO | Developer, DevOps | Resolve the avatar image URL GitLab would serve for a public email address; always answers 200, so this is not proof the account exists. |
| `gitlab_list_badges` | `IBadgesClient::ListForProjectAsync` | RO | Developer, DevOps | List a project's or group's badges, including group-inherited ones on a project (wraps ListForGroupAsync). |
| `gitlab_list_custom_attributes` | `ICustomAttributesClient::ListForProjectAsync` | RO | AdminOnly | List custom key/value attributes on a user, group, or project, selected by entity_type (wraps ListForUserAsync/ListForGroupAsync); administrator-only, reads included. |
| `gitlab_list_group_issues` | `IGroupsClient::ListIssuesAsync` | RO | Maintainer, Developer | List issues across a group and its subgroups, filtered by state, labels, assignee, milestone, iteration, weight, etc. |
| `gitlab_list_group_projects` | `IGroupsClient::ListProjectsAsync` | RO | Developer, DevOps | List projects a group owns, or projects shared into it, selected by a shared:bool (wraps ListSharedProjectsAsync). |
| `gitlab_list_namespace_storage_limit_exclusions` | `INamespacesClient::ListStorageLimitExclusionsAsync` | RO | DevOps, AdminOnly | List every namespace instance-wide that's excluded from storage-limit enforcement. |
| `gitlab_list_project_aliases` | `IProjectAliasesClient::ListAsync` | RO | DevOps, AdminOnly | List every Git-level project alias on the instance (Premium/Ultimate, administrators only). |
| `gitlab_list_project_forks` | `IProjectsClient::ListForksAsync` | RO | Developer | List a project's forks. |
| `gitlab_list_project_uploads` | `IProjectUploadsClient::ListAsync` | RO | Developer, DevOps | List markdown-attachment uploads on a project (requires Maintainer or Owner role). |
| `gitlab_list_storage_moves` | `IStorageMovesClient::ListForProjectAsync` | RO | DevOps, AdminOnly | List Gitaly repository-storage moves for a project, group, or snippet, or instance-wide when no entity id is given (wraps ListAllProjectMovesAsync/ListForGroupAsync/ListForSnippetAsync and group/snippet equivalents); self-managed instances only. |
| `gitlab_list_topics` | `ITopicsClient::ListAsync` | RO | Developer, DevOps | List instance-wide project topics, sorted by how many projects carry each one. |
| `gitlab_set_custom_attribute` | `ICustomAttributesClient::SetForProjectAsync` | WR | AdminOnly | Create or overwrite a custom attribute on a user, group, or project (wraps SetForUserAsync/SetForGroupAsync). |

### Pipelines, jobs, variables, triggers (`cicd`) — 16 tools, indicative profiles: DevOps (write) / Developer (read)

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_cancel_job` | `IJobsClient::CancelAsync` | WR | Developer, DevOps | Cancel a running or pending job. |
| `gitlab_create_ci_variable` | `IVariablesClient::CreateProjectVariableAsync` | WR | DevOps | Create a new CI/CD variable on a project or group (also covers CreateGroupVariableAsync). |
| `gitlab_create_pipeline` | `IPipelinesClient::CreateAsync` | WR | Developer, DevOps | Trigger a new pipeline run for a branch, tag or commit ref. |
| `gitlab_create_pipeline_schedule` | `IPipelineSchedulesClient::CreateAsync` | WR | DevOps | Create a new cron-triggered pipeline schedule for a ref. |
| `gitlab_create_trigger` | `ITriggersClient::CreateAsync` | WR | DevOps | Create a new pipeline trigger token; its value is only ever shown in full at creation. |
| `gitlab_delete_all_project_artifacts` | `IJobArtifactsClient::DeleteAllAsync` | DX | DevOps | Delete the artifacts of every job in a project (Maintainer+ role); GitLab processes the deletion in the background. |
| `gitlab_list_ci_variables` | `IVariablesClient::ListProjectVariablesAsync` | RO | DevOps | List the CI/CD variables defined on a project or group (scope parameter selects ListGroupVariablesAsync); values are already GitLab-redacted where masked. |
| `gitlab_list_job_artifacts` | `IJobArtifactsClient::ListAsync` | RO | Developer, DevOps | List the files inside a job's artifacts archive to find the exact path to fetch. |
| `gitlab_list_job_token_allowlist` | `IJobTokenScopeClient::ListAllowlistAsync` | RO | DevOps | List the projects or groups allowed to authenticate against this project using its CI job token (kind parameter selects ListGroupsAllowlistAsync). |
| `gitlab_list_jobs` | `IJobsClient::ListAsync` | RO | Developer, DevOps | List a project's jobs across pipelines, filterable by status/scope and ref. |
| `gitlab_list_my_pipelines` | `IPipelinesClient::ListForCurrentUserAsync` | RO | Developer, DevOps | List pipelines the authenticated token's user triggered, across every project they can see. |
| `gitlab_list_pipeline_bridge_jobs` | `IPipelinesClient::ListTriggerJobsAsync` | RO | Developer, DevOps | List the bridge jobs that start downstream pipelines from this one, to trace multi-project pipeline chains. |
| `gitlab_list_pipeline_jobs` | `IJobsClient::ListForPipelineAsync` | RO | Developer, DevOps | List the jobs that belong to one pipeline, to see per-stage pass/fail status. |
| `gitlab_list_pipeline_schedule_pipelines` | `IPipelineSchedulesClient::ListPipelinesAsync` | RO | DevOps | List the pipelines a given schedule has triggered, to confirm it is actually firing. |
| `gitlab_list_triggers` | `ITriggersClient::ListAsync` | RO | DevOps | List the pipeline trigger tokens configured on a project. |
| `gitlab_validate_ci_config` | `ICiLintClient::ValidateAsync` | RO | Developer, DevOps | Lint a .gitlab-ci.yml — either content you supply or, when omitted, what's already committed via ValidateProjectConfigurationAsync — returning validity, errors and the merged/expanded YAML. Never throws for invalid YAML; check the Valid field. |

### Package registries & container registry (`packages`) — 10 tools, indicative profiles: DevOps

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_container_registry_protection_rule` | `IProjectContainerRegistryProtectionRulesClient::CreateAsync` | WR | DevOps | Protect a container repository path pattern so only roles at or above a chosen level can push images to or delete images from repositories matching it. |
| `gitlab_create_package_protection_rule` | `IProjectPackageProtectionRulesClient::CreateAsync` | WR | DevOps | Lock down a package name pattern (e.g. 'release-*' for npm) so only roles at or above a chosen level can push or delete matching packages. |
| `gitlab_delete_container_repository` | `IContainerRegistryClient::DeleteRepositoryAsync` | DX | DevOps | Delete an entire container repository and every image tag it holds — decommissioning an image entirely. |
| `gitlab_delete_package` | `IPackagesGenericClient::DeletePackageAsync` | DX | DevOps | Remove a package and every file it owns from the registry, regardless of format — e.g. purging a bad or leaked release. |
| `gitlab_list_container_registry_protection_rules` | `IProjectContainerRegistryProtectionRulesClient::ListAsync` | RO | DevOps | List a project's container repository protection rules — which image repository path patterns only Maintainer+/Owner+ may push to or delete from. |
| `gitlab_list_container_repositories` | `IContainerRegistryClient::ListForProjectAsync` | RO | DevOps | List a project's container image repositories, optionally with tag counts and total size, to see what images the project publishes. |
| `gitlab_list_debian_distributions` | `IPackagesDebianClient::ListDistributionsForProjectAsync` | RO | DevOps | List a project's Debian (APT) repository distributions/codenames, e.g. to see which suites (stable, bullseye-security) are configured. |
| `gitlab_list_group_packages` | `IPackagesGenericClient::ListPackagesForGroupAsync` | RO | DevOps | Same cross-format browse as gitlab_list_packages but across every project in a group, e.g. an org-wide 'what have we published' sweep. |
| `gitlab_list_package_protection_rules` | `IProjectPackageProtectionRulesClient::ListAsync` | RO | DevOps | List a project's package protection rules — which package name patterns, by format, only Maintainer+ (or Owner+ for delete) may push or delete. |
| `gitlab_list_terraform_module_versions` | `IPackagesTerraformModulesClient::ListModuleVersionsAsync` | RO | DevOps | List every published version of a Terraform module with its dependency metadata, e.g. to decide which version a consumer should pin to. |

### Search, analytics, wiki, snippets, templates (`search`) — 12 tools, indicative profiles: Maintainer + Developer

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_add_code_search_indexed_namespace` | `ICodeSearchClient::AddIndexedNamespaceAsync` | WR | DevOps, AdminOnly | Add a namespace to a Zoekt node's exact-code-search index. |
| `gitlab_create_snippet` | `ISnippetsClient::CreateAsync` | WR | Developer | Create a personal snippet, or a project snippet (CreateForProjectAsync, Visibility required there) when a project is given. |
| `gitlab_create_wiki_page` | `IWikisClient::CreateForProjectAsync` | WR | Maintainer, Developer | Create a new wiki page (title + markdown content) in a project's or group's wiki. |
| `gitlab_disable_knowledge_graph_namespace` | `IKnowledgeGraphClient::DisableNamespaceAsync` | DX | DevOps, AdminOnly | Disable GitLab Duo Knowledge Graph for a namespace, removing its graph indexing. |
| `gitlab_list_all_snippets` | `ISnippetsClient::ListAllAsync` | RO | AdminOnly | List every snippet on the instance, personal and project alike. Requires Administrator or Auditor access. |
| `gitlab_list_code_review_analytics` | `IAnalyticsClient::ListCodeReviewAnalyticsAsync` | RO | Developer | Stream code-review metrics (review time, approver, diff stats) for a project's open merge requests, bounded by limit — for spotting stalled or slow-reviewed MRs. |
| `gitlab_list_code_search_indexed_namespaces` | `ICodeSearchClient::ListIndexedNamespacesAsync` | RO | DevOps, AdminOnly | List every namespace indexed on one Zoekt exact-code-search node. |
| `gitlab_list_knowledge_graph_namespaces` | `IKnowledgeGraphClient::ListNamespacesAsync` | RO | DevOps, AdminOnly | List every namespace with GitLab Duo Knowledge Graph enabled on this instance. |
| `gitlab_list_license_templates` | `ITemplatesClient::ListLicensesAsync` | RO | Maintainer, Developer | List available open-source license templates with metadata (key, name, popular flag); set popular-only to get the short common list. |
| `gitlab_list_search_migrations` | `ISearchClient::ListSearchMigrationsAsync` | RO | AdminOnly | List every advanced-search (Elasticsearch) migration known to the instance, for diagnosing search-index health. Admin token required. |
| `gitlab_list_wiki_pages` | `IWikisClient::ListForProjectAsync` | RO | Maintainer, Developer | List a project's or group's wiki pages (title/slug/format), optionally with content included; picks ListForGroupAsync when a group is given instead of a project. |
| `gitlab_update_active_context_collection` | `IActiveContextClient::UpdateCollectionAsync` | WR | DevOps, AdminOnly | Adjust an ActiveContext collection's queue-sharding options (shard count/limit) for scaling its indexing throughput. |

### Terraform, clusters, security, monitoring (`infra`) — 8 tools, indicative profiles: DevOps

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_cluster_agent_token` | `IClusterAgentsClient::CreateTokenAsync` | WR | DevOps | Issue a new authentication token for a cluster agent (max 2 active); the plaintext secret is returned exactly once for deploying to the agent. |
| `gitlab_create_error_tracking_client_key` | `IErrorTrackingClient::CreateClientKeyAsync` | WR | DevOps | Issue a new client key (with an auto-generated public key/DSN) for an application to report errors into GitLab's integrated error tracking. |
| `gitlab_create_terraform_state_protection_rule` | `ITerraformStatesClient::CreateProtectionRuleAsync` | WR | DevOps | Restrict who can write a named Terraform state to a minimum access level, optionally CI-only or protected-branch-CI-only. |
| `gitlab_download_attestation` | `IAttestationsClient::DownloadAsync` | RO | DevOps | Download one attestation's raw provenance bundle by its project-scoped id, for offline signature/provenance verification. |
| `gitlab_list_cluster_agent_tokens` | `IClusterAgentsClient::ListTokensAsync` | RO | DevOps | List an agent's active authentication tokens (metadata only, never the secret) to audit what is authorized to connect. |
| `gitlab_list_dependencies` | `IDependenciesClient::ListAsync` | RO | DevOps | List the dependencies (versions, package managers, known vulnerabilities/licenses) the last dependency-scanning job found in a project. |
| `gitlab_list_error_tracking_client_keys` | `IErrorTrackingClient::ListClientKeysAsync` | RO | DevOps | List the client keys issued for a project's integrated error tracking, to audit what is currently allowed to report errors in. |
| `gitlab_list_terraform_state_protection_rules` | `ITerraformStatesClient::ListProtectionRulesAsync` | RO | DevOps | List the write-access protection rules configured for a project's Terraform states. |

### Runners, environments, deployments, releases (`deploy`) — 14 tools, indicative profiles: DevOps

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_environment` | `IEnvironmentsClient::CreateAsync` | WR | DevOps | Register a new deployment environment on a project. |
| `gitlab_create_instance_deploy_key` | `IDeployKeysClient::CreateAsync` | WR | DevOps, AdminOnly | Create a deploy key directly on the instance (admin only), unattached to any project until enabled there. |
| `gitlab_create_pages_domain` | `IPagesClient::CreateDomainAsync` | WR | DevOps | Add a custom domain to a project's Pages site, with a supplied certificate or via Let's Encrypt auto-SSL. |
| `gitlab_create_release` | `IReleasesClient::CreateAsync` | WR | DevOps, Developer | Cut a new release from a tag, with notes and linked milestones. |
| `gitlab_create_runner_controller_token` | `IRunnerControllersClient::CreateTokenAsync` | WR | DevOps, AdminOnly | Create a new token record for a controller (metadata only - rotate it afterward to obtain a usable secret). |
| `gitlab_list_admin_deploy_keys` | `IDeployKeysClient::ListAllAsync` | RO | DevOps, AdminOnly | List every deploy key on the instance, or (given a user id) every project deploy key accessible to one user; admin only. Also covers ListForUserAsync. |
| `gitlab_list_admin_deploy_tokens` | `IDeployTokensClient::ListAsync` | RO | DevOps, AdminOnly | List every deploy token on the instance (admin only). |
| `gitlab_list_admin_runners` | `IRunnersClient::ListAllAsync` | RO | DevOps, AdminOnly | List every runner on the instance (admin/auditor only) for fleet-wide capacity or compliance review. |
| `gitlab_list_deployment_merge_requests` | `IDeploymentsClient::ListMergeRequestsAsync` | RO | DevOps, Developer | List the merge requests GitLab associates with a deployment, to see what code shipped in it. |
| `gitlab_list_group_releases` | `IReleasesClient::ListForGroupAsync` | RO | DevOps, Maintainer | List releases across every project in a group, optionally in a simplified shape. |
| `gitlab_list_instance_pages_domains` | `IPagesClient::ListAllDomainsAsync` | RO | DevOps, AdminOnly | List every Pages domain on the instance with its owning project and whether its certificate has expired (admin only). |
| `gitlab_list_runner_controller_scopes` | `IRunnerControllersClient::ListScopesAsync` | RO | DevOps, AdminOnly | List what a controller currently governs - the whole instance (RunnerId null) or specific runners. |
| `gitlab_list_secure_files` | `ISecureFilesClient::ListAsync` | RO | DevOps | List a project's secure files (CI/CD signing certificates, provisioning profiles, etc.) by metadata only. |
| `gitlab_update_runner` | `IRunnersClient::UpdateAsync` | WR | DevOps | Change a runner's description, tags, pause state, lock or timeout without recreating it. |

### Instance admin, hooks, integrations, audit (`admin`) — 14 tools, indicative profiles: DevOps + AdminOnly

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_feature_flag` | `IFeatureFlagsClient::CreateAsync` | WR | Developer, DevOps | Create a project feature flag, optionally with strategies already attached. |
| `gitlab_create_group_hook` | `IGroupHooksClient::CreateAsync` | WR | Developer, DevOps, AdminOnly | Add a webhook to a group, selecting which event types it fires on. |
| `gitlab_create_project_hook` | `IProjectHooksClient::AddAsync` | WR | Developer, DevOps, AdminOnly | Add a webhook to a project, selecting which event types it fires on. |
| `gitlab_create_system_hook` | `ISystemHooksClient::CreateAsync` | WR | AdminOnly | Register an instance-wide system hook that fires on events across every project and group. |
| `gitlab_get_instance_appearance` | `IInstanceClient::GetAppearanceAsync` | RO | AdminOnly | View the instance-wide sign-in/sign-up branding (logo, colors, messages). |
| `gitlab_list_audit_events` | `IAuditEventsClient::ListAsync` | RO | AdminOnly | List the instance-wide audit log, optionally filtered by entity type/id or date range (bounded). Requires administrator access. |
| `gitlab_list_background_migration_operations` | `IBackgroundMigrationsClient::ListOperationsAsync` | RO | DevOps, AdminOnly | List batched background operations on the instance (bounded) — the finer-grained unit a migration is made of. |
| `gitlab_list_feature_flag_user_lists` | `IFeatureFlagsClient::ListUserListsAsync` | RO | Developer, DevOps | List a project's feature flag user lists (bounded). |
| `gitlab_list_group_hook_deliveries` | `IGroupHooksClient::ListEventsAsync` | RO | Developer, DevOps, AdminOnly | List a group webhook's delivery log, newest first (7-day retention, bounded). |
| `gitlab_list_group_integrations` | `IIntegrationsClient::ListForGroupAsync` | RO | Developer, DevOps, AdminOnly | List a group's active third-party integrations, bounded. |
| `gitlab_list_license_policies` | `ILicensesClient::ListManagedAsync` | RO | DevOps, AdminOnly | List a project's software license compliance policies (allowed/denied verdicts), bounded. |
| `gitlab_list_project_hook_deliveries` | `IProjectHooksClient::ListEventsAsync` | RO | Developer, DevOps, AdminOnly | List a project webhook's delivery log, newest first, for debugging failed deliveries (7-day retention, bounded). |
| `gitlab_set_group_integration` | `IIntegrationsClient::SetForGroupAsync` | WR | Developer, DevOps, AdminOnly | Create or fully replace a group integration's settings by slug (same full-replace semantics as the project form). |
| `gitlab_set_license_policy` | `ILicensesClient::CreateManagedAsync` | WR | DevOps, AdminOnly | Create or update the allow/deny verdict for a software license on a project (upserts by license name; calls UpdateManagedAsync instead of CreateManagedAsync when the named policy already exists). |

### Import/export, ML, AI, misc (`lifecycle`) — 8 tools, indicative profiles: DevOps + AdminOnly

| Tool | Client :: Method | RO/W/D | Profiles | Purpose |
|---|---|---|---|---|
| `gitlab_create_bulk_import` | `IBulkImportsClient::CreateAsync` | WR | DevOps, AdminOnly | Start a direct-transfer migration of groups/projects from another GitLab instance -- GitLab's recommended migration path over file export/import. |
| `gitlab_create_ml_experiment` | `IMlExperimentsClient::CreateExperimentAsync` | WR | DevOps | Create a new MLflow-compatible experiment in a project. |
| `gitlab_duo_chat` | `IDuoClient::ChatAsync` | RO | Developer, Maintainer, DevOps | Ask GitLab Duo Chat a question, optionally scoped to an issue/MR/commit, without leaving the MCP session. |
| `gitlab_export_project` | `IProjectImportClient::ExportAsync` | WR | DevOps, AdminOnly | Schedule an async export of a project so its archive can later be downloaded or used to seed a migration. |
| `gitlab_get_group_export_download_info` | `IGroupImportClient::DownloadExportAsync` | RO | DevOps, AdminOnly | Confirm a scheduled group export archive is ready and report its size/name without transferring the archive bytes. |
| `gitlab_list_bulk_import_entities` | `IBulkImportsClient::ListEntitiesAsync, ListEntitiesForImportAsync` | RO | DevOps, AdminOnly | List the per-group/per-project entities of one migration, or of every migration when no import id is given, bounded by limit. |
| `gitlab_list_ml_experiments` | `IMlExperimentsClient::SearchExperimentsAsync` | RO | DevOps | List a project's MLflow-tracked experiments, paged/ordered, bounded by limit. |
| `gitlab_list_project_templates` | `IProjectImportClient::ListTemplatesAsync` | RO | DevOps, Developer, Maintainer | List the Dockerfile/.gitignore/CI/licence/issue/MR templates available to a project, bounded by limit. |
