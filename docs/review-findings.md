# Adversarial review — findings and fixes (2026-09-09)

A multi-dimension review pass (secret-field leakage, `GitLabContent` envelope correctness,
validation/hints, correctness/quality) read every file the 13-domain implementation pass produced,
grouped into 4 domain-batches × 4 dimensions (16 initial reviewer agents), followed by an independent
adversarial verification pass on every raw finding (one skeptic per finding, reading the file itself
before judging). Raw findings and the full verification transcript are in `review-findings.json`.

**35 raw findings → 29 confirmed real** (6 were refuted on independent re-read — a claim that didn't hold
up, or was already covered by a compiler error or the startup `ProfileGate.AssertCatalogIsComplete`
check). Of the 29 confirmed, **21 substantive (high/medium) findings were fixed** by a second wave of
domain-scoped fix agents, each self-verifying against a clean project build. 8 low-severity findings were
pure code-duplication (the same bounded-list boilerplate repeated across a file) and were left as known,
non-functional follow-up — the code is correct as written, just not maximally DRY.

## Fixed

- **[HIGH]** `DiscussionMapper` embedded every note of a discussion thread with no cap — fixed with a
  20-note-per-thread limit and a `Truncated` flag.
- **[HIGH]** `gitlab_list_wiki_pages(withContent: true)` returned unbounded per-page markdown — fixed
  with an 8,000-char per-page cap and a `ContentTruncated` flag.
- **[MEDIUM]** `gitlab_create_discussion`'s diff-anchor validation silently dropped `oldLine`/`newLine`
  when supplied without the SHA fields, producing an unanchored comment instead of an error — fixed.
- **[MEDIUM]** `gitlab_list_merge_train` never set `MergeTrainListOptions.Scope`, silently mixing
  already-merged cars into a "currently queued" result — fixed with an explicit `status` parameter.
- **[MEDIUM]** `gitlab_list_badges`/`gitlab_create_badge`: the mutual-exclusivity validation and the
  dispatch branch used different null/empty tests, so `project=""` with a real `group` silently queried
  the wrong (empty) project instead of erroring or routing to the group — fixed, both now agree.
- **[MEDIUM]** `GetFileBlameAsync`'s `rangeStart`/`rangeEnd` were documented as required together but
  never cross-validated — fixed.
- **[MEDIUM]** `CreateCurrentUserRunnerAsync`'s conditional `groupId`/`projectId` requirement (by
  `runnerType`) was undocumented in validation — fixed.
- **[MEDIUM]** `gitlab_cancel_job` set `Destructive=false`, contradicting `mcp-tool-authoring`'s own hint
  table (cancel/delete/merge/rotate → `Destructive=true`) — fixed.
- **[MEDIUM]** `CicdTools`' project/group scope selector silently treated any typo as `"project"`
  instead of validating — fixed with an explicit allow-list check.
- **[MEDIUM]** `CiLintResultSummary.MergedYaml` (a fully expanded `.gitlab-ci.yml`, potentially large for
  deep include chains) was unbounded — fixed with a 16,000-char cap and a `MergedYamlTruncated` flag.
- **[MEDIUM]** `gitlab_create_snippet`'s single-file/multi-file exclusivity check used OR instead of AND,
  so supplying only one of `fileName`/`content` passed validation incomplete — fixed.
- **[MEDIUM]** `ListTerraformModuleVersionsAsync`'s `Truncated` flag compared the outer (always-~1)
  module-entry count against `limit` instead of the actual per-module version count — fixed.
- **[MEDIUM]** `PagesMapper` silently dropped `VerificationCode` (the public DNS TXT-record value needed
  to finish GitLab Pages custom-domain verification) — added; confirmed not a secret via the XML docs.
- **[MEDIUM] → architectural, fixed repo-wide.** `Grant.AdminOnly` combined with a persona bit
  (`Grant.DevOps | Grant.AdminOnly`) is reachable under that persona **directly** —
  `Grant.IsVisibleIn` checks each bit independently (an OR of independent conditions), not an AND-gate,
  so combining a persona bit with `AdminOnly` never restricts anything beyond what the persona bit alone
  already grants; `FullPermission`'s own check (`grant != Grant.None`) means the persona bit alone is
  already sufficient for `FullPermission` visibility too. This pattern appeared, independently, in 6 of
  13 domain implementations (`Search`, `Admin` — found by the review; `Deploy`, `Lifecycle`, `People`,
  `Projects` — found by a follow-up sweep of the same pattern across every `ProfileCatalog.*.cs` file
  after the first two were fixed). Every combined row was re-judged against its tool's own
  `[Description]`: rows whose description states "requires administrator access" became bare
  `Grant.AdminOnly`; rows with no such language kept their persona bit and dropped the now-inert
  `AdminOnly` bit. Golden snapshots (`tests/snapshots/`) were regenerated after this fix — `DevOps` moved
  from 111 to 92 tools (12 previously-leaky admin-only tools are no longer directly reachable under a
  bare `DevOps` profile) and `Maintainer` moved from 53 to 52 (`gitlab_list_member_roles` correctly
  narrowed to instance-administrator-only).

## Left as known follow-up (low severity, code quality only)

The bounded-list boilerplate (limit-range check, `PerPage = Math.Min(limit+1, MaxLimit)`, break-before-add
truncation loop) is repeated near-verbatim across most list tools in `CodeTools.cs`, `PeopleTools.cs`,
`ProjectsTools.cs`, `PackagesTools.cs`, `MergeRequestsTools.cs`, `DiscussionTools.cs`, `LifecycleTools.cs`,
and a comma-splitting helper is near-duplicated across `DeployTools.cs`/`AdminTools.cs`/`LifecycleTools.cs`
with one subtle divergence in edge-case handling (an all-commas input). None of this is a correctness bug
— every occurrence was independently verified byte-for-byte correct — it is pure duplication a shared
helper in a common base class or extension method would remove. Left unfixed to keep this pass scoped to
actual defects; a good first task for anyone extending this codebase from the `tool-catalog-backlog.json`
backlog.

## Verified after fixes (first wave, 188 tools)

- `dotnet build GitlabMCP.slnx -c Release` — 0 warnings, 0 errors.
- `dotnet publish -c Release -r win-x64` — 0 IL2xxx/IL3xxx warnings, binary runs and answers 188 tools.
- `docker build` for `linux-musl-x64` — see the Dockerfile/image for the current result.
- Golden per-profile snapshots regenerated: `tests/snapshots/{Maintainer,Developer,DevOps,FullPermission}.{tools,prompts}.txt`.

---

## Second wave — the 542-tool backlog implementation (2026-09-09)

After `tool-catalog-backlog.json`'s full 542 remaining candidates were implemented (13 domains in
parallel, ~18 tools per agent call, sequential within a domain), the identical review discipline ran
again, scaled to the finer grain the stakes warranted: **13 domains × 5 dimensions = 65 initial reviewer
agents** (the same 4 dimensions as the first wave, plus a 5th — profile-grant correctness — added
specifically because the first wave's most consequential finding was exactly that category, DEC-027).
Each reviewer read only the newly-added code for one domain and one dimension. Raw findings were then
independently verified (one skeptic per finding, reading the file itself) before being handed to one
fix agent per domain.

**46 raw findings → 40 confirmed real** (6 refuted on independent re-read). All 40 confirmed findings were
fixed, across all 13 domains, in a single fix wave — every fix agent self-verified its own reasoning
against the actual GitLab.Client XML docs or a DLL reflection probe before changing code, not from memory.
A controlled build-fix loop (one build-check agent, then parallel per-file fix agents on distinct files
only) reached a clean build in 2 iterations.

Selected fixes, by category:

- **Envelope/unbounded-content (6 findings).** `MergeRequestResult.Description`,
  `FileContentResult.Content`, `BlameRangeSummary.Lines`, `TestCaseSummary.SystemOutput`/`StackTrace`,
  `TemplateDetail.Content`/`LicenseTemplateDetail.Content`, `ActiveContextCollectionResult.OptionsJson`,
  `TerraformModuleSummary.Versions`, and `ProjectTemplateDetail.Content` were all unbounded GitLab text or
  lists with no cap/truncation flag — each fixed with a domain-appropriate character/line/count cap and a
  `*Truncated` bool, following the exact pattern already established by the first wave's
  `DiscussionMapper`/wiki-page fixes.
- **Grant/profile-gate corrections (7 findings, DEC-027 enforcement, not new violations).**
  `gitlab_list_group_credential_inventory`, `gitlab_manage_group_credential`,
  `gitlab_list_group_audit_events` were bare `Grant.AdminOnly` despite their own `[Description]` stating a
  non-admin access path (group Owner) — corrected to `Grant.DevOps`, per DEC-027's rule applied correctly
  this time. `gitlab_create_personal_access_token`, `gitlab_add_ssh_key`, `gitlab_list_project_members`
  were granted narrower than their own description and sibling tools warranted (missing `Maintainer` or
  `Everyone`) — widened to match. None of these are a recurrence of DEC-027's original bug (no row combined
  `AdminOnly` with a persona bit); a repo-wide grep for that exact pattern after this wave found zero
  matches outside a pre-existing comment.
- **Wrong documented defaults (4 findings).** `gitlab_list_group_merge_requests`/`gitlab_list_merge_requests`/
  `gitlab_list_my_merge_requests`'s `state` parameter and `gitlab_list_group_issues`'s `state` parameter all
  claimed a default GitLab does not actually apply (verified against `MergeRequestListOptions`/group-issues
  API docs) — descriptions corrected to state the real default.
  `ProtectBranchAsync`/`ProtectTagAsync`'s access-level descriptions listed a non-existent `50=Owner` level
  — corrected against `GitLab.Client.xml`'s actual enumerated values.
- **Missing tool (1 finding, net new).** `gitlab_list_discussion_notes` did not exist — `IDiscussionsClient`'s
  five `ListNotesIn*DiscussionAsync` methods were never called anywhere in the tree, so there was no way to
  page through a discussion thread's notes past `DiscussionMapper`'s 20-note cap. Added as a new tool,
  granted `Grant.Planning` alongside its siblings; `gitlab_list_discussions`/`gitlab_get_discussion`'s
  descriptions corrected to stop claiming "every note" and point at the new tool instead.
  **This moved the total tool count from 730 to 731.**
- **Destructive/Idempotent hint corrections (4 findings).** `gitlab_rotate_personal_access_token`,
  `gitlab_rotate_resource_access_token`, `gitlab_update_pages_domain` (credential/certificate rotation) and
  `gitlab_cancel_pipeline` (cancels every running/pending job) were all `Destructive=false`, contradicting
  `mcp-tool-authoring`'s own hint table — corrected to `true`. `gitlab_create_ai_agent_session` was
  `Idempotent=false` despite documenting the same idempotency-key retry semantics as its already-`true`
  siblings — corrected.
- **Wrong/incomplete data (5 findings).** `AdminMapper`'s hook-event collector omitted 7–8 real boolean
  event flags GitLab.Client actually exposes on project/group hooks (found by reflecting the live DLL, one
  more flag than the raw finding claimed). `RemoveMemberAsync`'s `unassignIssuables` description had the
  true/false meaning backwards relative to the real GitLab.Client option. `ProjectMapper` hardcoded
  `TwoFactorEnabled` to `null` despite the source model exposing a real value.
  `ListMlExperimentsAsync`/`ListMlModelsAsync`'s `Math.Min(limit + 1, MaxLimit)` collapsed to `limit`
  exactly at `limit=100` (since `MaxLimit` is also 100), silently eating the probe item truncation
  detection needs — replaced with `limit + 1` (GitLab's real server-side cap is far higher, so no
  additional clamp was needed). `gitlab_list_pipeline_bridge_jobs` labelled its envelope provenance with a
  deprecated route string instead of the one it actually calls. Two `Uri`-parsing/missing-field gaps in the
  `infra` domain (an uncaught `UriFormatException`, two silently-dropped real fields on a cluster agent
  token) were fixed to match sibling methods' existing patterns.

## Verified after fixes (second wave, 731 tools)

- `dotnet build GitlabMCP.slnx -c Release` — 0 warnings, 0 errors (both the workflow's own build-fix loop
  and an independent rebuild after it completed).
- A repo-wide grep for `Grant.AdminOnly` combined with any persona bit on one row — zero matches outside a
  pre-existing comment string (DEC-027 has not regressed).
- `dotnet publish -c Release -r win-x64` — 0 IL2xxx/IL3xxx warnings, binary runs and answers all 731 tools
  plus 9 prompts and 9 resources (2 direct + 7 templated).
- `docker build` for `linux-musl-x64` — 0 warnings; container run, checked against `/healthz` and a live
  `tools/list` (731) / `resources/templates/list` (7) over its forwarded port.
- Golden per-profile snapshots regenerated for all four primitive kinds:
  `tests/snapshots/{Maintainer,Developer,DevOps,FullPermission}.{tools,prompts,resources,resource_templates}.txt`
  — tool counts moved Maintainer 146→150, Developer 335→336, DevOps 433→436, FullPermission 730→731, each
  change traceable to one of the grant-widening fixes or the new tool above.
